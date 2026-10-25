using Vitral.Ui.Barra;

namespace Vitral.Ui.Tests;

public sealed class PainelWifiTests
{
    private readonly WifiFalso _wifi = new();

    private PainelWifiViewModel Criar() => new(_wifi);

    private static RedeWifi Rede(PainelWifiViewModel vm, string nome) => vm.Redes.Single(r => r.Nome == nome);

    [Fact]
    public void Ordem_conectada_e_sinal()
    {
        using var vm = Criar();
        Assert.Equal(["Casa", "Escritorio", "Casa 2.4", "Vizinho", "Aberta"], vm.Redes.Select(r => r.Nome));
    }

    [Fact]
    public async Task Rede_protegida_nova_pede_senha()
    {
        using var vm = Criar();
        await vm.EscolherCommand.ExecuteAsync(Rede(vm, "Vizinho"));
        Assert.Equal("Vizinho", vm.PedindoSenha?.Nome);
        Assert.Empty(_wifi.Conexoes);
    }

    [Fact]
    public async Task Rede_salva_conecta_sem_senha()
    {
        using var vm = Criar();
        await vm.EscolherCommand.ExecuteAsync(Rede(vm, "Casa 2.4"));
        Assert.Equal([("Casa 2.4", (string?)null)], _wifi.Conexoes);
    }

    [Fact]
    public async Task Rede_aberta_conecta_sem_senha()
    {
        using var vm = Criar();
        await vm.EscolherCommand.ExecuteAsync(Rede(vm, "Aberta"));
        Assert.Equal([("Aberta", (string?)null)], _wifi.Conexoes);
    }

    [Fact]
    public async Task Senha_errada_mostra_mensagem_e_mantem_o_campo()
    {
        _wifi.Resposta = new ResultadoConexao(false, "Senha incorreta.");
        using var vm = Criar();
        await vm.EscolherCommand.ExecuteAsync(Rede(vm, "Vizinho"));
        vm.Senha = "errada";
        await vm.ConectarComSenhaCommand.ExecuteAsync(null);
        Assert.Equal([("Vizinho", (string?)"errada")], _wifi.Conexoes);
        Assert.Equal("Senha incorreta.", vm.Mensagem);
        Assert.Equal("Vizinho", vm.PedindoSenha?.Nome);
        Assert.Equal("", vm.Senha);
    }

    [Fact]
    public async Task Senha_certa_fecha_o_campo()
    {
        using var vm = Criar();
        await vm.EscolherCommand.ExecuteAsync(Rede(vm, "Vizinho"));
        vm.Senha = "certa";
        await vm.ConectarComSenhaCommand.ExecuteAsync(null);
        Assert.Null(vm.PedindoSenha);
        Assert.Null(vm.Mensagem);
    }

    [Fact]
    public async Task Conectada_desconecta()
    {
        using var vm = Criar();
        await vm.EscolherCommand.ExecuteAsync(Rede(vm, "Casa"));
        Assert.Equal(1, _wifi.Desconexoes);
    }

    [Fact]
    public async Task Corporativa_abre_configuracoes()
    {
        using var vm = Criar();
        await vm.EscolherCommand.ExecuteAsync(Rede(vm, "Escritorio"));
        Assert.Equal(["ms-settings:network-wifi"], _wifi.Abertos);
        Assert.Empty(_wifi.Conexoes);
    }

    [Fact]
    public void Sem_permissao_de_localizacao_mostra_aviso()
    {
        _wifi.Atual = _wifi.Atual! with { PermissaoLocalizacao = false };
        using var vm = Criar();
        Assert.True(vm.AvisoLocalizacao);
        vm.AbrirLocalizacaoCommand.Execute(null);
        Assert.Equal(["ms-settings:privacy-location"], _wifi.Abertos);
    }

    [Fact]
    public void Chave_liga_e_desliga()
    {
        using var vm = Criar();
        Assert.True(vm.Ligado);
        vm.Ligado = false;
        Assert.Equal([false], _wifi.Ligacoes);
    }

    [Fact]
    public void Aviso_da_fonte_nao_volta_como_ligar()
    {
        using var vm = Criar();
        _wifi.Atual = _wifi.Atual! with { RadioLigado = false };
        _ = _wifi.AtualizarAsync();
        Assert.False(vm.Ligado);
        Assert.Empty(_wifi.Ligacoes);
    }

    [Fact]
    public void Sem_adaptador()
    {
        _wifi.Atual = new EstadoWifi(false, false, true, []);
        using var vm = Criar();
        Assert.False(vm.TemAdaptador);
        Assert.True(vm.SemAdaptador);
        Assert.Empty(vm.Redes);
        vm.AbrirMaisCommand.Execute(null);
        Assert.Equal(["ms-settings:network-wifi"], _wifi.Abertos);
    }

    [Fact]
    public async Task Abrir_atualiza_a_lista()
    {
        using var vm = Criar();
        await vm.AbrirAsync();
        Assert.Equal(1, _wifi.Atualizacoes);
    }

}

public sealed class RevisaoWifiTests
{
    [Fact]
    public async Task Senha_errada_com_a_lista_atualizada_mantem_o_campo()
    {
        var wifi = new WifiFalso { Resposta = new ResultadoConexao(false, "Senha incorreta."), AtualizarAoConectar = true };
        using var vm = new PainelWifiViewModel(wifi);
        await vm.EscolherCommand.ExecuteAsync(vm.Redes.Single(r => r.Nome == "Vizinho"));
        vm.Senha = "errada";
        await vm.ConectarComSenhaCommand.ExecuteAsync(null);

        // A lista nova traz registros novos: a rede continua pedindo senha (por valor; a tela não pode comparar referência).
        Assert.Equal("Senha incorreta.", vm.Mensagem);
        Assert.Equal("Vizinho", vm.PedindoSenha?.Nome);
        Assert.Contains(vm.PedindoSenha, vm.Redes);
    }
}
