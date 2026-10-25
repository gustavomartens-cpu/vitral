using Vitral.Core.Barra;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;

namespace Vitral.Core.Aplicacao;

/// Aplica os ajustes nativos de um perfil com retrato antes. Escrita bloqueada (UCPD) é listada e não interrompe;
/// qualquer outra falha desfaz o que já foi gravado e é relançada. Com <paramref name="barra"/>, a seção barraTopo
/// do perfil (e a do retrato, ao reverter) também é aplicada; sem ele (CLI), a barra não é tocada.
public sealed class Aplicador(IRegistro registro, ServicoRetratos retratos, INotificador notificador, EstadoBarra? barra = null)
{
    private const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    public Resultado Aplicar(Perfil p)
    {
        retratos.GarantirOriginal();
        var anterior = retratos.Tirar(p.Nome);
        string arquivoAnterior = retratos.Salvar(anterior);

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
            ReverterRegistro(anterior); // a barra ainda não foi tocada
            throw;
        }

        barra?.Aplicar(p.BarraTopo);
        Avisar(mudouBarra: aplicados.Any(EhAlinhamento));
        return new Resultado(naoAplicados.Count == 0, aplicados, naoAplicados, anterior, arquivoAnterior);
    }

    /// Volta ao estado do retrato: valores ausentes são apagados, os outros regravados.
    /// Devolve o que o Windows não deixou voltar (spec §6: nunca relatar sucesso parcial como total).
    public IReadOnlyList<(ValorRegistro Valor, string Motivo)> Reverter(Retrato r)
    {
        var naoVoltou = ReverterRegistro(r);
        barra?.Aplicar(r.BarraTopo);
        return naoVoltou;
    }

    private List<(ValorRegistro Valor, string Motivo)> ReverterRegistro(Retrato r)
    {
        var naoVoltou = new List<(ValorRegistro, string)>();
        foreach (var v in r.Valores)
        {
            try
            {
                if (v.Ausente) registro.Apagar(v.Chave, v.Nome);
                else registro.Gravar(v);
            }
            catch (EscritaBloqueadaException e)
            {
                naoVoltou.Add((v, e.Message));
            }
        }
        Avisar(mudouBarra: r.Valores.Any(EhAlinhamento));
        return naoVoltou;
    }

    /// Volta a um retrato guardando antes o estado atual, para que a própria volta possa ser desfeita
    /// (usado por "desfazer" e "original").
    public IReadOnlyList<(ValorRegistro Valor, string Motivo)> Restaurar(Retrato alvo, string rotulo)
    {
        retratos.Salvar(retratos.Tirar(rotulo));
        return Reverter(alvo);
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
