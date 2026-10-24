namespace Vitral.Core.Registro;

/// Acesso ao registro do usuário (HKCU). Isola o Windows para que o núcleo seja testável.
public interface IRegistro
{
    /// Devolve o valor, ou um <see cref="ValorRegistro"/> com Dado nulo se não existir.
    ValorRegistro Ler(string chave, string nome, TipoValor tipoEsperado);

    /// Grava o valor. Lança <see cref="EscritaBloqueadaException"/> quando o Windows nega o acesso.
    void Gravar(ValorRegistro valor);

    /// Apaga o valor, se existir.
    void Apagar(string chave, string nome);
}
