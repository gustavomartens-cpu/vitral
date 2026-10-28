using Vitral.Core.Barra;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;

namespace Vitral.Core.Aplicacao;

/// Aplica os ajustes nativos de um perfil com retrato antes. Escrita bloqueada (UCPD) é listada e não interrompe;
/// qualquer outra falha desfaz o que já foi gravado e é relançada. Com <paramref name="barra"/>, a seção barraTopo
/// do perfil (e a do retrato, ao reverter) também é aplicada; sem ele (CLI), a barra não é tocada.
/// Com <paramref name="icones"/>, a lista de ícones aplicada fica guardada para os ícones que aparecerem depois.
public sealed class Aplicador(IRegistro registro, ServicoRetratos retratos, INotificador notificador, EstadoBarra? barra = null,
    EstadoIcones? icones = null, EstadoDoca? doca = null, Aparencia.EstadoAparencia? aparencia = null, Formas.EstadoFormas? formas = null)
{

    public Resultado Aplicar(Perfil p)
    {
        retratos.GarantirOriginal();
        var anterior = retratos.Tirar(p.Nome);
        string arquivoAnterior = retratos.Salvar(anterior);

        var valores = new List<ValorRegistro>(p.Nativo is null ? [] : AjustesNativos.Calcular(p.Nativo));
        var aplicados = new List<ValorRegistro>();
        var naoAplicados = new List<(ValorRegistro, string)>();
        if (p.BarraTarefas is { } barraTarefas)
        {
            var stuck = registro.Ler(BarraDeTarefas.StuckRects, "Settings", TipoValor.Binario).Dado as byte[];
            valores.AddRange(BarraDeTarefas.Calcular(barraTarefas, stuck));
            if (barraTarefas.OcultarAutomaticamente is not null && BarraDeTarefas.OcultarAutomatico(stuck) is null)
                naoAplicados.Add((new ValorRegistro(BarraDeTarefas.StuckRects, "Settings", TipoValor.Binario, null),
                    "Configuração da barra não encontrada."));
            if (barraTarefas.IconesVisiveis is { } lista) valores.AddRange(Icones.Calcular(registro, lista));
        }
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

        // Aparência e formas antes da barra e da doca: as janelas que ligam agora já nascem com elas.
        aparencia?.Aplicar(p.Aparencia);
        formas?.Aplicar(p.Formas);
        barra?.Aplicar(p.BarraTopo);
        doca?.Aplicar(p.BarraBaixo);
        icones?.Definir(p.BarraTarefas?.IconesVisiveis); // sem lista no perfil = não mexer: o observador para
        Avisar(aplicados);
        return new Resultado(naoAplicados.Count == 0, aplicados, naoAplicados, anterior, arquivoAnterior);
    }

    /// Volta ao estado do retrato: valores ausentes são apagados, os outros regravados.
    /// Devolve o que o Windows não deixou voltar (spec §6: nunca relatar sucesso parcial como total).
    public IReadOnlyList<(ValorRegistro Valor, string Motivo)> Reverter(Retrato r)
    {
        // A lista volta antes do registro: o Voltar apaga IsPromoted que não existiam, e o observador não pode
        // tratá-los como ícones novos com a lista que está saindo.
        icones?.Definir(r.IconesVisiveis);
        var naoVoltou = ReverterRegistro(r);
        aparencia?.Aplicar(r.Aparencia);
        formas?.Restaurar(r.Formas); // substitui tudo: campo que estava em "não mexer" volta também
        barra?.Aplicar(r.BarraTopo);
        doca?.Aplicar(r.BarraBaixo);
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
        Avisar(r.Valores);
        return naoVoltou;
    }

    /// Volta a um retrato guardando antes o estado atual, para que a própria volta possa ser desfeita
    /// (usado por "desfazer" e "original").
    public IReadOnlyList<(ValorRegistro Valor, string Motivo)> Restaurar(Retrato alvo, string rotulo)
    {
        retratos.Salvar(retratos.Tirar(rotulo));
        return Reverter(alvo);
    }

    /// Tema sempre; "TraySettings" se mudou algo da barra; e o aviso de ocultar automaticamente quando o StuckRects3
    /// foi gravado (o Explorer não relê o registro sozinho e, ao desligar, nem regrava o byte — teste de 25/10).
    private void Avisar(IEnumerable<ValorRegistro> gravados)
    {
        notificador.AvisarMudancaDeTema();
        if (gravados.Any(v => BarraDeTarefas.EhDaBarra(v) || Icones.EhDeIcone(v))) notificador.AvisarMudancaDaBarra();
        if (gravados.FirstOrDefault(BarraDeTarefas.EhStuckRects) is { } stuck
            && BarraDeTarefas.OcultarAutomatico(stuck.Dado as byte[]) is { } ocultar
            && doca?.AbsorverOcultarAutomatico(ocultar) != true) // com a doca ligada, vale para quando ela desligar
            notificador.AvisarOcultarAutomatico(ocultar);
    }
}
