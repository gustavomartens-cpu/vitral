namespace Vitral.Ui.Barra;

/// As mensagens de AppBar (SHAppBarMessage) que a reserva manda ao Windows, separadas para teste.
public interface IAppBar
{
    /// ABM_NEW. Falso se o Windows recusar, inclusive quando a janela já está registrada.
    bool Novo();

    /// ABM_REMOVE. Sem efeito se a janela não estiver registrada.
    void Remover();

    /// ABM_QUERYPOS + ABM_SETPOS.
    void Posicionar();
}

/// Quando a reserva (ReservaFaixa) está registrada e quando registrar de novo.
public sealed class RegistroAppBar(IAppBar appBar)
{
    public bool Registrada { get; private set; }

    /// Registra (se preciso) e posiciona. Devolve false se o Windows recusar.
    public bool Registrar()
    {
        if (!Registrada)
        {
            if (!appBar.Novo()) return false;
            Registrada = true;
        }
        appBar.Posicionar();
        return true;
    }

    public void Reposicionar()
    {
        if (Registrada) appBar.Posicionar();
    }

    public void Liberar()
    {
        if (!Registrada) return;
        appBar.Remover();
        Registrada = false;
    }

    /// Chegou um "TaskbarCreated": o próximo <see cref="Registrar"/> registra de novo. Ele vem do Explorer reiniciado,
    /// que já não conhece a reserva, mas também da bandeja do Vitral (ManagedShell) ao ligar e ao devolver os ícones,
    /// com a reserva ainda viva: ali o ABM_NEW falharia e a reserva ficaria parada onde estava (a doca acima da faixa
    /// da barra do Windows, no notebook de 25/10). Por isso remove antes.
    public void AoCriarBarra()
    {
        appBar.Remover();
        Registrada = false;
    }
}
