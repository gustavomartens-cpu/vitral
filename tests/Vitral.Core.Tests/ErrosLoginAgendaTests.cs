using System.Net;
using Vitral.Core.Agenda;

namespace Vitral.Core.Tests;

public sealed class ErrosLoginAgendaTests
{
    [Fact]
    public void API_desligada_no_projeto_diz_onde_ligar()
    {
        const string corpo = """{"error":{"code":403,"message":"Google Calendar API has not been used in project 281945787259 before or it is disabled.","errors":[{"reason":"accessNotConfigured"}],"status":"PERMISSION_DENIED"}}""";
        Assert.Contains("API do Google Agenda está desligada", RegrasAgenda.ErroDoGoogle(HttpStatusCode.Forbidden, corpo));
    }

    [Fact]
    public void Sem_resposta_do_servidor_e_sem_conexao() =>
        Assert.Equal("Sem conexão com o Google. Confira a internet e tente de novo.", RegrasAgenda.ErroDoGoogle(null, null));

    [Fact]
    public void Outro_erro_mostra_o_codigo() =>
        Assert.Equal("O Google respondeu com erro 500. Tente de novo em alguns minutos.", RegrasAgenda.ErroDoGoogle(HttpStatusCode.InternalServerError, "x"));
}
