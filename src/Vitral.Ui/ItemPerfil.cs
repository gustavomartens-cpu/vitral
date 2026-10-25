namespace Vitral.Ui;

/// Uma linha da lista de perfis: nome, cor da gema (a cor de destaque do perfil ou o azul do logo) e se é o ativo.
public sealed record ItemPerfil(string Nome, string CorGema, bool Ativo)
{
    public const string CorPadrao = "#4C7DF0";
}
