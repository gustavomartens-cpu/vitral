using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;

namespace Vitral.Core.Aplicacao;

/// Aplica os ajustes nativos de um perfil com retrato antes. Escrita bloqueada (UCPD) é listada e não interrompe;
/// qualquer outra falha desfaz o que já foi gravado e é relançada.
public sealed class Aplicador(IRegistro registro, ServicoRetratos retratos, INotificador notificador)
{
    private const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    public Resultado Aplicar(Perfil p)
    {
        retratos.GarantirOriginal();
        var anterior = retratos.Tirar(p.Nome);
        retratos.Salvar(anterior);

        var valores = p.Nativo is null ? [] : AjustesNativos.Calcular(p.Nativo);
        var aplicados = new List<ValorRegistro>();
        var naoAplicados = new List<(ValorRegistro, string)>();
        try
        {
            foreach (var v in valores)
            {
                try
                {
                    registro.Gravar(v);
                    aplicados.Add(v);
                }
                catch (EscritaBloqueadaException e)
                {
                    naoAplicados.Add((v, e.Message));
                }
            }
        }
        catch
        {
            Reverter(anterior);
            throw;
        }

        Avisar(mudouBarra: aplicados.Any(EhAlinhamento));
        return new Resultado(naoAplicados.Count == 0, aplicados, naoAplicados, anterior);
    }

    /// Volta ao estado do retrato: valores ausentes são apagados, os outros regravados.
    public void Reverter(Retrato r)
    {
        foreach (var v in r.Valores)
        {
            try
            {
                if (v.Ausente) registro.Apagar(v.Chave, v.Nome);
                else registro.Gravar(v);
            }
            catch (EscritaBloqueadaException)
            {
                // Se não conseguimos gravar, também não conseguimos ter mudado: nada a desfazer.
            }
        }
        Avisar(mudouBarra: r.Valores.Any(EhAlinhamento));
    }

    private void Avisar(bool mudouBarra)
    {
        notificador.AvisarMudancaDeTema();
        if (mudouBarra) notificador.AvisarMudancaDaBarra();
    }

    private static bool EhAlinhamento(ValorRegistro v) =>
        string.Equals(v.Chave, Advanced, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(v.Nome, "TaskbarAl", StringComparison.OrdinalIgnoreCase);
}
