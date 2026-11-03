using Vitral.Core.Claude;
using Vitral.Core.Controles;
using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

internal sealed class ControlesFalsos : IFonteControles
{
    public EstadoControles Estado { get; set; } = new(true, false, false, false,
        [new TelaBrilho("notebook", "Tela do notebook", 40), new TelaBrilho("externo", "Monitor externo", 60)],
        [new AparelhoBluetooth("1", "JBL", TipoAparelho.Audio, false, true), new AparelhoBluetooth("2", "BT5.0 KB", TipoAparelho.Teclado, true, false)]);
    public List<string> Chamadas { get; } = [];
    /// Nulo: devolve o Estado na hora. Preenchido: a leitura espera a tarefa (para testar leituras sobrepostas).
    public Func<Task<EstadoControles>>? LerCom { get; set; }
    public Task<EstadoControles> Ler() { Chamadas.Add("ler"); return LerCom?.Invoke() ?? Task.FromResult(Estado); }
    public Task<bool> Bluetooth(bool ligar) { Chamadas.Add($"bluetooth {ligar}"); Estado = Estado with { Bluetooth = ligar }; return Task.FromResult(true); }
    public Task<bool> Aviao(bool ligar) { Chamadas.Add($"aviao {ligar}"); Estado = Estado with { Aviao = ligar }; return Task.FromResult(true); }
    public Task<bool> NaoPerturbe(bool ligar) { Chamadas.Add($"naoperturbe {ligar}"); Estado = Estado with { NaoPerturbe = ligar }; return Task.FromResult(true); }
    public Task<bool> LuzNoturna(bool ligar) { Chamadas.Add($"luz {ligar}"); Estado = Estado with { LuzNoturna = ligar }; return Task.FromResult(true); }
    public void Brilho(string telaId, int valor) => Chamadas.Add($"brilho {telaId} {valor}");
    public Task<bool> Aparelho(AparelhoBluetooth a) { Chamadas.Add($"aparelho {a.Nome}"); return Task.FromResult(true); }
}

public sealed class PainelControlesTests
{
    private DateTime _agora = new(2026, 11, 3, 10, 0, 0);
    private readonly ControlesFalsos _fonte = new();
    private PainelControlesViewModel Criar() => new(_fonte, () => _agora);

    [Fact]
    public async Task Abrir_le_o_estado()
    {
        var p = Criar();
        await p.Abrir();
        Assert.True(p.Bluetooth);
        Assert.False(p.Aviao);
        Assert.Equal(["notebook", "externo"], p.Telas.Select(t => t.Id));
        Assert.Equal(2, p.Aparelhos.Count);
    }

    [Fact]
    public async Task Interruptor_chama_a_fonte_e_rele()
    {
        var p = Criar();
        await p.Abrir();
        await p.AlternarBluetoothCommand.ExecuteAsync(null);
        Assert.Equal(["ler", "bluetooth False", "ler"], _fonte.Chamadas);
        Assert.False(p.Bluetooth);
        await p.AlternarAviaoCommand.ExecuteAsync(null);
        Assert.True(p.Aviao);
    }

    [Fact]
    public async Task Indisponivel_fica_apagado()
    {
        _fonte.Estado = _fonte.Estado with { Bluetooth = null, NaoPerturbe = null };
        var p = Criar();
        await p.Abrir();
        Assert.Null(p.Bluetooth);
        await p.AlternarBluetoothCommand.ExecuteAsync(null);
        Assert.DoesNotContain(_fonte.Chamadas, c => c.StartsWith("bluetooth"));
    }

    [Fact]
    public async Task Luz_noturna_desconhecida_mostra_o_texto()
    {
        _fonte.Estado = _fonte.Estado with { LuzNoturna = null };
        var p = Criar();
        await p.Abrir();
        Assert.Equal("Abra a luz noturna nas Configurações do Windows", p.TextoLuzNoturna);
        _fonte.Estado = _fonte.Estado with { LuzNoturna = false };
        await p.Abrir();
        Assert.Null(p.TextoLuzNoturna);
    }

    [Fact]
    public async Task Aviso_do_teclado()
    {
        var p = Criar();
        await p.Abrir();
        Assert.True(p.AvisoTeclado); // o BT5.0 KB está conectado
        _fonte.Estado = _fonte.Estado with { Aparelhos = [] };
        await p.Abrir();
        Assert.False(p.AvisoTeclado);
    }

    [Fact]
    public async Task Brilho_arrastado_junta_as_escritas()
    {
        var p = Criar();
        await p.Abrir();
        p.MudarBrilho("notebook", 50);
        _agora = _agora.AddMilliseconds(30);
        p.MudarBrilho("notebook", 55);
        _agora = _agora.AddMilliseconds(30);
        p.MudarBrilho("notebook", 60);
        Assert.Equal(["brilho notebook 50"], _fonte.Chamadas.Where(c => c.StartsWith("brilho")));
        _agora = _agora.AddMilliseconds(120);
        p.Tique();
        Assert.Equal(["brilho notebook 50", "brilho notebook 60"], _fonte.Chamadas.Where(c => c.StartsWith("brilho")));
        p.Tique();
        Assert.Equal(2, _fonte.Chamadas.Count(c => c.StartsWith("brilho"))); // nada pendente
    }

    [Fact]
    public async Task Releitura_descarta_o_brilho_pendente()
    {
        // O pendente era de uma tela que pode ter sumido ou mudado de lugar (sobras dos controles).
        var p = Criar();
        await p.Abrir();
        p.MudarBrilho("externo", 50);
        _agora = _agora.AddMilliseconds(30);
        p.MudarBrilho("externo", 70);
        await p.Abrir();
        _agora = _agora.AddMilliseconds(200);
        p.Tique();
        Assert.Equal(["brilho externo 50"], _fonte.Chamadas.Where(c => c.StartsWith("brilho")));
    }

    [Fact]
    public async Task Fechar_grava_o_ultimo_brilho_na_hora()
    {
        var p = Criar();
        await p.Abrir();
        p.MudarBrilho("notebook", 50);
        _agora = _agora.AddMilliseconds(30);
        p.MudarBrilho("notebook", 80);
        p.Descarregar();
        Assert.Equal(["brilho notebook 50", "brilho notebook 80"], _fonte.Chamadas.Where(c => c.StartsWith("brilho")));
    }

    [Fact]
    public async Task Leitura_antiga_que_termina_depois_e_descartada()
    {
        var p = Criar();
        var lenta = new TaskCompletionSource<EstadoControles>();
        _fonte.LerCom = () => lenta.Task;
        var primeira = p.Abrir(); // a abertura do painel, lenta
        _fonte.LerCom = null;
        _fonte.Estado = _fonte.Estado with { Bluetooth = false };
        await p.Abrir(); // a releitura depois de um clique
        lenta.SetResult(_fonte.Estado with { Bluetooth = true });
        await primeira;
        Assert.False(p.Bluetooth);
    }

    [Fact]
    public async Task Telas_iguais_nao_recriam_os_deslizantes()
    {
        // Recriar no meio de um arrasto solta o deslizante da mão do usuário.
        var p = Criar();
        await p.Abrir();
        int mudancas = 0;
        p.Telas.CollectionChanged += (_, _) => mudancas++;
        _fonte.Estado = _fonte.Estado with { Bluetooth = false };
        await p.Abrir();
        Assert.Equal(0, mudancas);
    }

    [Fact]
    public async Task Tela_que_sumiu_sai_da_lista()
    {
        var p = Criar();
        await p.Abrir();
        _fonte.Estado = _fonte.Estado with { Telas = [new TelaBrilho("notebook", "Tela do notebook", 40)] };
        await p.Abrir();
        Assert.Equal(["notebook"], p.Telas.Select(t => t.Id));
    }

    [Fact]
    public async Task Nao_perturbe_mudado_fora_avisa_ao_reler()
    {
        // Ligado pelo Win+A: a releitura do painel leva o estado ao notch (revisão dos controles, I2).
        var p = Criar();
        bool? avisado = null;
        p.MudouNaoPerturbe += v => avisado = v;
        _fonte.Estado = _fonte.Estado with { NaoPerturbe = true };
        await p.Abrir();
        Assert.True(avisado);
        _fonte.Estado = _fonte.Estado with { NaoPerturbe = null };
        await p.Abrir();
        Assert.True(avisado); // ilegível não mexe no notch
    }

    [Fact]
    public async Task Nao_perturbe_avisa()
    {
        var p = Criar();
        await p.Abrir();
        bool? avisado = null;
        p.MudouNaoPerturbe += v => avisado = v;
        await p.AlternarNaoPerturbeCommand.ExecuteAsync(null);
        Assert.True(avisado);
    }

    [Fact]
    public void Nao_perturbe_nao_desce_notificacao_mas_desce_o_Claude()
    {
        var e = new EstadoNotch(() => _agora) { NaoPerturbe = true };
        e.Notificou(new AvisoNotificacao("Chrome", "Google Chrome", "Oi", null));
        Assert.Equal(ModoNotch.Normal, e.Modo);
        e.Chegou(new AvisoClaude(TipoAviso.Terminou, "vitral", "Pronto."));
        Assert.Equal(ModoNotch.Aviso, e.Modo);
    }

    [Fact]
    public void Microfone_mudo_e_troca()
    {
        var mic = new MicrofoneFalso();
        var disp = new DispositivosFalsos();
        var p = new PainelVolumeViewModel(new VolumeFalso(), disp, mic);
        Assert.True(p.MostrarMicrofone);
        Assert.False(p.MicrofoneMudo);
        p.AlternarMicrofoneCommand.Execute(null);
        Assert.True(p.MicrofoneMudo);
        Assert.Equal(["mic-usb"], p.Entradas.Where(e => !e.Padrao).Select(e => e.Id));
        p.EscolherEntradaCommand.Execute(p.Entradas.First(e => !e.Padrao));
        Assert.Equal(["mic-usb"], disp.EntradasTornadas);
        Assert.True(p.MostrarEntradas);
    }

    [Fact]
    public void Lista_de_microfones_some_sem_poder_trocar()
    {
        // Sem a troca do Windows, a lista só confundiria: o clique não faria nada (sobras dos controles).
        var p = new PainelVolumeViewModel(new VolumeFalso(), new DispositivosFalsos { PodeTrocar = false }, new MicrofoneFalso());
        Assert.True(p.MostrarMicrofone); // o mudo continua
        Assert.False(p.MostrarEntradas);
    }
}

internal sealed class MicrofoneFalso : IFonteMicrofone
{
    public bool? Mudo { get; private set; } = false;
    public event Action? Mudou;
    public void AlternarMudo() { Mudo = !Mudo; Mudou?.Invoke(); }
}
