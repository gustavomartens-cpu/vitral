using Vitral.Core.Aparencia;
using Vitral.Core.Perfis;

namespace Vitral.Core.Tests;

public sealed class AcertosAparenciaTests
{
    [Fact]
    public void Aviso_do_Windows_sem_mudanca_nao_recalcula()
    {
        var vigia = new VigiaWindows();
        Assert.True(vigia.Mudou(new VisualWindows(true, "#0078D4", true)));
        Assert.False(vigia.Mudou(new VisualWindows(true, "#0078D4", true)));
        Assert.True(vigia.Mudou(new VisualWindows(false, "#0078D4", true)));
        Assert.True(vigia.Mudou(new VisualWindows(false, "#0078D4", false)));
    }

    [Fact]
    public void Valor_de_enum_desconhecido_vira_nao_mexer()
    {
        const string json = """
            {
              "esquema": 4, "nome": "Futuro",
              "formas": { "doca": { "formato": "espiral", "icones": "grande" }, "notch": { "formato": "ilha" } },
              "aparencia": { "tema": "grafite", "geral": { "cantos": "hexagonais", "corDestaque": "#FF8A8A" } }
            }
            """;
        var p = PerfilJson.Ler(json);
        Assert.Null(p.Formas!.Doca!.Formato);
        Assert.Equal(TamanhoIcones.Grande, p.Formas.Doca.Icones);
        Assert.Equal(FormatoNotch.Ilha, p.Formas.Notch!.Formato);
        Assert.Equal(TemaAparencia.Grafite, p.Aparencia!.Tema);
        Assert.Null(p.Aparencia.Geral!.Cantos);
        Assert.Equal("#FF8A8A", p.Aparencia.Geral.CorDestaque);
    }

    [Fact]
    public void Valor_de_enum_desconhecido_em_campo_obrigatorio_ainda_recusa()
    {
        const string json = """{ "esquema": 4, "nome": "Futuro", "aparencia": { "tema": "holografico" } }""";
        Assert.ThrowsAny<Exception>(() => PerfilJson.Ler(json));
    }
}
