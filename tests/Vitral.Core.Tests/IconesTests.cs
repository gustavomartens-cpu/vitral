using Vitral.Core.Aplicacao;
using Vitral.Core.Barra;
using Vitral.Core.Configuracao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;

namespace Vitral.Core.Tests;

public sealed class IconesTests : IDisposable
{
    private const string Raiz = Icones.Chave;
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "vitral-ic-" + Guid.NewGuid().ToString("N"));
    private readonly RegistroEmMemoria _registro = new();
    private readonly ServicoConfig _config;
    private readonly EstadoIcones _estado;
    private readonly ServicoRetratos _retratos;
    private readonly Aplicador _aplicador;

    public IconesTests()
    {
        Icone("1", @"{6D809377-6AF0-444B-8957-A3773F02200E}\Spotify\Spotify.exe", "Spotify", promovido: 1);
        Icone("2", @"C:\Program Files (x86)\Steam\steam.exe", null, promovido: 0);
        Icone("3", @"C:\Program Files\Wallpaper Engine\wallpaper64.exe", "Wallpaper Engine", promovido: null);
        _config = new ServicoConfig(Path.Combine(_pasta, "config.json"));
        _estado = new EstadoIcones(_config);
        _retratos = new ServicoRetratos(_registro, Path.Combine(_pasta, "retratos"));
        _aplicador = new Aplicador(_registro, _retratos, new NotificadorNulo(), icones: _estado);
    }

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    private void Icone(string id, string exe, string? dica, uint? promovido)
    {
        string chave = $@"{Raiz}\{id}";
        _registro.Gravar(new ValorRegistro(chave, "ExecutablePath", TipoValor.Texto, exe));
        if (dica is not null) _registro.Gravar(new ValorRegistro(chave, "InitialTooltip", TipoValor.Texto, dica));
        if (promovido is not null) _registro.Gravar(new ValorRegistro(chave, "IsPromoted", TipoValor.Dword, promovido));
    }

    private uint? Promovido(string id) => _registro.Ler($@"{Raiz}\{id}", "IsPromoted", TipoValor.Dword).Dado as uint?;

    private static Perfil Com(params string[] visiveis) => new(3, "I", null, null, null, new BarraTarefasPerfil(IconesVisiveis: visiveis));

    [Fact]
    public void SubChaves_lista_um_nivel()
    {
        Assert.Equal(["1", "2", "3"], _registro.SubChaves(Raiz).Order());
        Assert.Empty(_registro.SubChaves(@"Control Panel\NaoExiste"));
    }

    [Fact]
    public void Programas_com_nome_da_dica_ou_do_executavel()
    {
        var p = Icones.Programas(_registro);
        Assert.Equal(["Spotify", "steam", "Wallpaper Engine"], p.Select(x => x.Nome));
        Assert.Equal("Spotify.exe", p[0].Executavel);
    }

    [Fact]
    public void Calcular_promove_so_os_da_lista_sem_diferenciar_maiusculas()
    {
        _aplicador.Aplicar(Com("SPOTIFY.EXE"));
        Assert.Equal(1u, Promovido("1"));
        Assert.Equal(0u, Promovido("2"));
        Assert.Equal(0u, Promovido("3"));
    }

    [Fact]
    public void Lista_vazia_esconde_todos()
    {
        _aplicador.Aplicar(Com());
        Assert.All(["1", "2", "3"], id => Assert.Equal(0u, Promovido(id)));
    }

    [Fact]
    public void Retrato_e_reversao_dos_icones()
    {
        var r = _aplicador.Aplicar(Com("steam.exe"));
        Assert.Equal(1u, Promovido("2"));
        _aplicador.Reverter(r.RetratoAnterior);
        Assert.Equal(1u, Promovido("1"));
        Assert.Equal(0u, Promovido("2"));
        Assert.Null(Promovido("3"));
    }

    [Fact]
    public void Aplicar_guarda_a_lista_e_reverter_limpa()
    {
        var r = _aplicador.Aplicar(Com("steam.exe"));
        Assert.Equal(["steam.exe"], _estado.Atual()!);
        _aplicador.Reverter(r.RetratoAnterior);
        Assert.Null(_estado.Atual());
    }

    [Fact]
    public void Icone_novo_recebe_pela_lista()
    {
        Icone("4", @"C:\Discord\Discord.exe", "Discord", promovido: null);
        Assert.Equal(0u, Icones.ParaNovo(_registro, "4", ["steam.exe"])!.Dado);
        Assert.Equal(1u, Icones.ParaNovo(_registro, "4", ["DISCORD.exe"])!.Dado);
        Assert.Null(Icones.ParaNovo(_registro, "1", ["steam.exe"])); // já tem IsPromoted
    }

    [Fact]
    public void Aplicar_novos_usa_a_lista_guardada()
    {
        _estado.Definir(["steam.exe"]);
        Icone("4", @"C:\Discord\Discord.exe", "Discord", promovido: null);
        Assert.Equal(2, Icones.AplicarNovos(_registro, _estado)); // wallpaper64 e Discord, que não tinham IsPromoted
        Assert.Equal(0u, Promovido("4"));
    }

    [Fact]
    public void Nao_mexer_nao_observa()
    {
        Icone("4", @"C:\Discord\Discord.exe", "Discord", promovido: null);
        Assert.Equal(0, Icones.AplicarNovos(_registro, _estado));
        Assert.Null(Promovido("4"));
    }

    private sealed class NotificadorNulo : INotificador
    {
        public void AvisarMudancaDeTema() { }
        public void AvisarMudancaDaBarra() { }
        public void AvisarOcultarAutomatico(bool ligado) { }
    }
}
