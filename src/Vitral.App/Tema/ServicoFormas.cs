using System.ComponentModel;
using System.Windows;
using Vitral.Core.Formas;
using Vitral.Core.Perfis;

namespace Vitral.App.Tema;

/// As formas da doca, do notch e das ilhas (spec das formas §5.1): guarda os valores resolvidos e avisa quem desenha. O
/// raio do notch vem do "Cantos" da aparência, então recalcula quando a aparência muda.
internal sealed class ServicoFormas : IControleFormas, INotifyPropertyChanged
{
    public static ServicoFormas Instancia { get; } = new();

    private FormasPerfil _perfil = EstadoFormas.Padrao;
    private readonly ResourceDictionary _doca = [];

    private ServicoFormas()
    {
        Recalcular();
        ServicoAparencia.Instancia.Mudou += Recalcular;
    }

    public ValoresFormas Atual { get; private set; } = null!;

    /// Os valores mudaram: as partes se reposicionam e redesenham.
    public event Action? Mudou;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// Os ícones dos apps da doca em preto e branco (spec de 17/11/2026); a doca liga a camada cinza por binding.
    public bool IconesCinza { get; private set; }

    public void Aplicar(FormasPerfil formas)
    {
        _perfil = formas;
        Recalcular();
    }

    /// A doca passa a ler os tamanhos dos ícones (DocaIcone, DocaImagem...) e a ampliação.
    public void RegistrarDoca(FrameworkElement elemento)
    {
        if (!elemento.Resources.MergedDictionaries.Contains(_doca)) elemento.Resources.MergedDictionaries.Add(_doca);
    }

    private void Recalcular()
    {
        var novo = ResolverFormas.Resolver(_perfil, ServicoAparencia.Instancia.Valores(ParteAparencia.Notch).Cantos);
        if (novo == Atual) return;
        Atual = novo;
        var d = novo.Doca;
        _doca["DocaIcone"] = d.Icone;
        _doca["DocaImagem"] = d.Imagem;
        _doca["DocaLogo"] = Math.Round(d.Imagem * 22 / 24); // inteiros: tamanho fracionário borra o desenho
        _doca["DocaFonteBusca"] = Math.Round(d.Icone * 16 / 30);
        _doca["DocaFundoBotao"] = d.Icone + 6;
        _doca["DocaBotaoLargura"] = d.BotaoLargura;
        _doca["DocaBotaoAltura"] = d.BotaoAltura;
        _doca["DocaAmpliacao"] = d.Ampliacao;
        bool cinza = d.CorIcones == CorIconesDoca.PretoEBranco;
        if (cinza != IconesCinza)
        {
            IconesCinza = cinza;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconesCinza)));
        }
        Mudou?.Invoke();
    }
}
