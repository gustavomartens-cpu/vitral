namespace Vitral.Core.Aparencia;

/// O Windows avisa "preferência mudou" muitas vezes sem mudar o modo, o destaque nem a transparência: só a leitura
/// diferente da anterior redesenha doca, notch e ilhas (revisão final da 0.10.0).
public sealed class VigiaWindows
{
    private VisualWindows? _ultima;

    public bool Mudou(VisualWindows agora)
    {
        if (agora == _ultima) return false;
        _ultima = agora;
        return true;
    }
}
