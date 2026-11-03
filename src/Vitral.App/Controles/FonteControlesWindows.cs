using System.Diagnostics;
using Vitral.Core.Configuracao;
using Vitral.Core.Controles;
using Vitral.Ui.Barra;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

namespace Vitral.App.Controles;

/// Os controles do Windows para o painel (spec dos controles §4), cada um pelo caminho provado. Tudo roda fora da thread
/// da interface, e cada falha vira false/nulo com log: o painel nunca recebe uma exceção.
internal sealed class FonteControlesWindows(ServicoConfig config) : IFonteControles
{
    public Task<EstadoControles> Ler() => Task.Run(async () =>
    {
        // Radios, Seguro e Aparelhos já seguram as próprias falhas; o config tem a trava dele.
        var radios = await Radios();
        bool? bluetooth = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth) is { } bt ? bt.State == RadioState.On : null;
        return new EstadoControles(bluetooth, config.Ler().RadiosAntesDoAviao is not null, Seguro(Controles.NaoPerturbe.Ler),
            Seguro(Controles.LuzNoturna.Ler), Seguro(Controles.Brilho.Ler) ?? [], await Aparelhos());
    });

    public Task<bool> Bluetooth(bool ligar) => Rodar("Controles.Bluetooth", async () =>
    {
        var bt = (await Radios()).FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
        return bt is not null && await bt.SetStateAsync(ligar ? RadioState.On : RadioState.Off) == RadioAccessStatus.Allowed;
    });

    /// Ligado: guarda o estado de cada rádio no config e desliga todos. Desligado: religa os que estavam ligados antes;
    /// a lista só sai do config quando todos voltaram (senão o próximo clique tenta de novo).
    public Task<bool> Aviao(bool ligar) => Rodar("Controles.Aviao", async () =>
    {
        var radios = await Radios();
        if (radios.Count == 0) return false; // sem acesso aos rádios: não finge que ligou
        var agora = radios.GroupBy(r => r.Kind.ToString()).ToDictionary(g => g.Key, g => g.Any(r => r.State == RadioState.On));
        if (ligar)
        {
            config.Atualizar(c => c with { RadiosAntesDoAviao = RegrasControles.Aviao(agora) });
            foreach (var r in radios) await r.SetStateAsync(RadioState.Off);
            return true;
        }
        if (config.Ler().RadiosAntesDoAviao is not { } guardado) return true;
        var religar = RegrasControles.DevolverAviao(guardado, agora);
        bool todos = true;
        foreach (var r in radios.Where(r => religar.Contains(r.Kind.ToString())))
            todos &= await r.SetStateAsync(RadioState.On) == RadioAccessStatus.Allowed;
        if (todos) config.Atualizar(c => c with { RadiosAntesDoAviao = null });
        return todos;
    });

    public Task<bool> NaoPerturbe(bool ligar) => Rodar("Controles.NaoPerturbe", () => Task.FromResult(Controles.NaoPerturbe.Definir(ligar)));

    public Task<bool> LuzNoturna(bool ligar) => Rodar("Controles.LuzNoturna", () => Task.FromResult(Controles.LuzNoturna.Definir(ligar)));

    /// Um de cada vez por tela, só o último valor (o DDC/CI é lento e escritas sobrepostas chegam fora de ordem).
    private readonly FilaUltimoValor<string, int> _brilho =
        new(Controles.Brilho.Definir, e => Barra.LogBarra.Erro("Controles.Brilho", e));

    public void Brilho(string telaId, int valor) => _brilho.Pedir(telaId, valor);

    /// Fone ou caixa: conecta ou desconecta pelo áudio; os outros abrem a tela de Bluetooth do Windows.
    public Task<bool> Aparelho(AparelhoBluetooth a) => Rodar("Controles.Aparelho", async () =>
    {
        if (a.TemAudio && await BluetoothDevice.FromIdAsync(a.Id) is { } bt
            && FonesBluetooth.FiltroDe(bt.BluetoothAddress, FonesBluetooth.Filtros()) is { } filtro)
            return FonesBluetooth.Pedir(filtro, conectar: !a.Conectado) == 0;
        Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        return true;
    });

    /// Fora da thread da interface; qualquer falha vira false com log (o painel nunca recebe uma exceção).
    private static Task<bool> Rodar(string onde, Func<Task<bool>> fazer) => Task.Run(async () =>
    {
        try { return await fazer(); }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro(onde, e); return false; }
    });

    private static async Task<IReadOnlyList<Radio>> Radios()
    {
        try
        {
            if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed) return [];
            return await Radio.GetRadiosAsync();
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("Controles.Radios", e); return []; }
    }

    private static async Task<IReadOnlyList<AparelhoBluetooth>> Aparelhos()
    {
        var lista = new List<(ulong, AparelhoBluetooth)>();
        try
        {
            var filtros = FonesBluetooth.Filtros();
            foreach (var d in await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true)))
            {
                if (await BluetoothDevice.FromIdAsync(d.Id) is not { } bt) continue;
                uint classe = bt.ClassOfDevice?.RawValue ?? 0;
                var tipo = RegrasControles.TipoPelaClasse((int)((classe >> 8) & 0x1F), (int)((classe >> 2) & 0x3F), le: false, d.Name);
                lista.Add((bt.BluetoothAddress, new AparelhoBluetooth(d.Id, d.Name, tipo, bt.ConnectionStatus == BluetoothConnectionStatus.Connected,
                    FonesBluetooth.FiltroDe(bt.BluetoothAddress, filtros) is not null)));
            }
            // O LE vem depois: com o mesmo endereço de um clássico (o celular), o Juntar fica com o clássico, que tem o
            // áudio, se nenhum dos dois estiver conectado.
            foreach (var d in await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)))
            {
                if (await BluetoothLEDevice.FromIdAsync(d.Id) is not { } le) continue;
                lista.Add((le.BluetoothAddress, new AparelhoBluetooth(d.Id, d.Name, RegrasControles.TipoPelaClasse(0, 0, le: true, d.Name),
                    le.ConnectionStatus == BluetoothConnectionStatus.Connected, TemAudio: false)));
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("Controles.Aparelhos", e); }
        return RegrasControles.Juntar(lista);
    }

    private static T? Seguro<T>(Func<T> ler)
    {
        try { return ler(); }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("Controles", e); return default; }
    }
}
