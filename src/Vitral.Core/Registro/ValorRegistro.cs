namespace Vitral.Core.Registro;

public enum TipoValor { Dword, Binario, Texto }

/// Um valor do registro do usuário (HKCU). <see cref="Chave"/> é relativa a HKCU.
/// <see cref="Dado"/> nulo significa que o valor não existe ("ausente").
/// Dword = uint, Binario = byte[], Texto = string.
public sealed record ValorRegistro(string Chave, string Nome, TipoValor Tipo, object? Dado)
{
    public bool Ausente => Dado is null;

    public bool Equals(ValorRegistro? outro) =>
        outro is not null &&
        string.Equals(Chave, outro.Chave, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Nome, outro.Nome, StringComparison.OrdinalIgnoreCase) &&
        Tipo == outro.Tipo &&
        DadosIguais(Dado, outro.Dado);

    public override int GetHashCode() =>
        HashCode.Combine(Chave.ToUpperInvariant(), Nome.ToUpperInvariant(), Tipo);

    private static bool DadosIguais(object? a, object? b) => (a, b) switch
    {
        (null, null) => true,
        (byte[] x, byte[] y) => x.AsSpan().SequenceEqual(y),
        _ => Equals(a, b),
    };
}

/// O Windows negou a escrita (por exemplo, o UCPD protegendo a chave).
public sealed class EscritaBloqueadaException(ValorRegistro valor, Exception? causa = null)
    : Exception($@"Escrita bloqueada pelo Windows: HKCU\{valor.Chave}\{valor.Nome}", causa)
{
    public ValorRegistro Valor { get; } = valor;
}
