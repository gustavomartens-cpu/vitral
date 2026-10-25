using CommunityToolkit.Mvvm.ComponentModel;

namespace Vitral.Ui;

/// Uma linha da lista de perfis: nome, cor da gema (a cor de destaque do perfil ou o azul do logo) e se é o ativo.
/// Cor e ativo mudam na própria linha: trocar a linha na coleção faria a ListBox soltar a seleção.
public sealed partial class ItemPerfil(string nome, string corGema, bool ativo) : ObservableObject
{
    public const string CorPadrao = "#4C7DF0";

    public string Nome { get; } = nome;

    [ObservableProperty] private string _corGema = corGema;
    [ObservableProperty] private bool _ativo = ativo;
}
