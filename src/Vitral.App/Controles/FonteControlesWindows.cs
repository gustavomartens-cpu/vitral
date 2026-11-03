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
        var radios = await Radios();
        bool? bluetooth = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth) is { } bt ? bt.State == RadioState.On : null;
        return new EstadoControles(bluetooth, config.Ler().RadiosAntesDoAviao is not null, Seguro(Controles.NaoPerturbe.Ler),
            Seguro(Controles.LuzNoturna.Ler), Seguro(Controles.Brilho.Ler) ?? [], await Aparelhos());
    });

    public Task<bool> Bluetooth(bool ligar) => Task.Run(async () =>
    {
        var bt = (await Radios()).FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
        return bt is not null && await bt.SetStateAsync(ligar ? RadioState.On : RadioState.Off) == RadioAccessStatus.Allowed;
    });

    /// Ligado: guarda o estado de cada rádio no config e desliga todos. Desligado: religa os que estavam ligados antes.
    public Task<bool> Aviao(bool ligar) => Task.Run(async () =>
    {
        var radios = await Radios();
        var agora = radios.GroupBy(r => r.Kind.ToString()).ToDictionary(g => g.Key, g => g.Any(r => r.State == RadioState.On));
        if (ligar)
        {
            config.Gravar(config.Ler() with { RadiosAntesDoAviao = RegrasControles.Aviao(agora) });
            foreach (var r in radios) await r.SetStateAsync(RadioState.Off);
            return true;
        }
        if (config.Ler().RadiosAntesDoAviao is not { } guardado) return true;
        var religar = RegrasControles.DevolverAviao(guardado, agora);
        foreach (var r in radios.Where(r => religar.Contains(r.Kind.ToString()))) await r.SetStateAsync(RadioState.On);
        config.Gravar(config.Ler() with { RadiosAntesDoAviao = null });
        return true;
    });

    public bool NaoPerturbe(bool ligar) => Seguro(() => Controles.NaoPerturbe.Definir(ligar));

    public bool LuzNoturna(bool ligar) => Seguro(() => Controles.LuzNoturna.Definir(ligar));

    public void Brilho(string telaId, int valor) => Task.Run(() => Controles.Brilho.Definir(telaId, valor));

    /// Fone ou caixa: conecta ou desconecta pelo áudio; os outros abrem a tela de Bluetooth do Windows.
    public Task<bool> Aparelho(AparelhoBluetooth a) => Task.Run(async () =>
    {
        if (a.TemAudio && await BluetoothDevice.FromIdAsync(a.Id) is { } bt
            && FonesBluetooth.FiltroDe(bt.BluetoothAddress, FonesBluetooth.Filtros()) is { } filtro)
            return FonesBluetooth.Pedir(filtro, conectar: !a.Conectado) == 0;
        Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        return true;
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
        var lista = new List<AparelhoBluetooth>();
        try
        {
            var filtros = FonesBluetooth.Filtros();
            foreach (var d in await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true)))
            {
                if (await BluetoothDevice.FromIdAsync(d.Id) is not { } bt) continue;
                uint classe = bt.ClassOfDevice?.RawValue ?? 0;
                var tipo = RegrasControles.TipoPelaClasse((int)((classe >> 8) & 0x1F), (int)((classe >> 2) & 0x3F), le: false, d.Name);
                lista.Add(new AparelhoBluetooth(d.Id, d.Name, tipo, bt.ConnectionStatus == BluetoothConnectionStatus.Connected,
                    FonesBluetooth.FiltroDe(bt.BluetoothAddress, filtros) is not null));
            }
            foreach (var d in await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)))
            {
                if (lista.Any(a => a.Nome == d.Name)) continue; // o mesmo aparelho pelos dois lados (o celular)
                if (await BluetoothLEDevice.FromIdAsync(d.Id) is not { } le) continue;
                lista.Add(new AparelhoBluetooth(d.Id, d.Name, RegrasControles.TipoPelaClasse(0, 0, le: true, d.Name),
                    le.ConnectionStatus == BluetoothConnectionStatus.Connected, TemAudio: false));
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("Controles.Aparelhos", e); }
        // O mesmo aparelho LE aparece duas vezes às vezes (o teclado): fica um, o conectado se houver.
        return lista.GroupBy(a => (a.Nome, a.Tipo)).Select(g => g.OrderByDescending(a => a.Conectado).First())
            .OrderByDescending(a => a.Conectado).ThenBy(a => a.Nome).ToList();
    }

    private static T? Seguro<T>(Func<T> ler)
    {
        try { return ler(); }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("Controles", e); return default; }
    }

    private static bool Seguro(Func<bool> fazer)
    {
        try { return fazer(); }
        catch (Exception e) when (e is not OutOfMemoryException) { Barra.LogBarra.Erro("Controles", e); return false; }
    }
}
