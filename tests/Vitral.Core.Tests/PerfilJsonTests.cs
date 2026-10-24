using Vitral.Core.Perfis;

namespace Vitral.Core.Tests;

public class PerfilJsonTests
{
    // Exemplo da spec §4.2 (a seção "barra" pertence à fase 1b e deve ser ignorada aqui).
    private const string ExemploSpec = """
        {
          "esquema": 1,
          "nome": "Vidro escuro",
          "buildTestada": "26200.9550",
          "nativo": {
            "modoSistema": "escuro", "modoApps": "escuro",
            "corDestaque": "#21375E", "corDestaqueAutomatica": false,
            "transparencia": true,
            "destaqueNaBarraEIniciar": true, "destaqueNosTitulos": true,
            "alinhamentoBarra": "centro",
            "posicaoBarra": null, "barraPequena": null
          },
          "barra": {
            "estilo": "tema",
            "tema": { "origem": "ramensoftware/windows-11-taskbar-styling-guide", "nome": "FrostyGlass", "versao": "abc123" }
          }
        }
        """;

    [Fact]
    public void Le_o_exemplo_da_spec()
    {
        var p = PerfilJson.Ler(ExemploSpec);
        Assert.Equal("Vidro escuro", p.Nome);
        Assert.Equal(1, p.Esquema);
        Assert.Equal("26200.9550", p.BuildTestada);
        Assert.NotNull(p.Nativo);
        Assert.Equal("#21375E", p.Nativo!.CorDestaque);
        Assert.Equal(Modo.Escuro, p.Nativo.ModoSistema);
        Assert.Equal(Alinhamento.Centro, p.Nativo.AlinhamentoBarra);
        Assert.False(p.Nativo.CorDestaqueAutomatica);
    }

    [Fact]
    public void Ida_e_volta_preserva_o_perfil()
    {
        var p = PerfilJson.Ler(ExemploSpec);
        var de_novo = PerfilJson.Ler(PerfilJson.Escrever(p));
        Assert.Equal(p, de_novo);
    }

    [Fact]
    public void Campo_nulo_continua_nulo()
    {
        var p = PerfilJson.Ler("""{ "esquema": 1, "nome": "x", "nativo": { "modoApps": "claro" } }""");
        Assert.Equal(Modo.Claro, p.Nativo!.ModoApps);
        Assert.Null(p.Nativo.ModoSistema);
        Assert.Null(p.Nativo.CorDestaque);
        Assert.Null(p.Nativo.Transparencia);
    }

    [Fact]
    public void Escrever_usa_camelCase_e_enum_em_texto()
    {
        var json = PerfilJson.Escrever(new Perfil(1, "x", null,
            new AjustesNativosPerfil(Modo.Escuro, null, null, null, null, null, null, Alinhamento.Esquerda)));
        Assert.Contains("\"modoSistema\": \"escuro\"", json);
        Assert.Contains("\"alinhamentoBarra\": \"esquerda\"", json);
    }

    [Fact]
    public void Recusa_esquema_futuro()
    {
        var ex = Assert.Throws<PerfilInvalidoException>(() => PerfilJson.Ler("""{ "esquema": 2, "nome": "x" }"""));
        Assert.Contains("esquema 2", ex.Message);
    }

    [Fact]
    public void Recusa_cor_invalida_citando_o_campo()
    {
        var ex = Assert.Throws<PerfilInvalidoException>(() =>
            PerfilJson.Ler("""{ "esquema": 1, "nome": "x", "nativo": { "corDestaque": "azul" } }"""));
        Assert.Contains("corDestaque", ex.Message);
    }

    [Fact]
    public void Recusa_json_quebrado()
    {
        Assert.Throws<PerfilInvalidoException>(() => PerfilJson.Ler("{ \"esquema\": 1, "));
    }

    [Fact]
    public void Recusa_perfil_sem_nome()
    {
        Assert.Throws<PerfilInvalidoException>(() => PerfilJson.Ler("""{ "esquema": 1 }"""));
    }
}
