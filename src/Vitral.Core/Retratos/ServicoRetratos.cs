using System.Text.Json;
using Vitral.Core.Nativo;
using Vitral.Core.Registro;

namespace Vitral.Core.Retratos;

/// Tira, guarda e lista retratos em uma pasta. Mantém os 10 mais recentes;
/// o original.json (estado do Windows antes do primeiro uso) nunca é apagado.
public sealed class ServicoRetratos(IRegistro registro, string pasta)
{
    private const int Limite = 10;
    private const string NomeOriginal = "original.json";

    public Retrato Tirar(string? perfil) => new(
        DateTimeOffset.Now,
        perfil,
        AjustesNativos.Controlados.Select(c => registro.Ler(c.Chave, c.Nome, c.Tipo)).ToList());

    public void Salvar(Retrato r)
    {
        Directory.CreateDirectory(pasta);
        // Sequencial com 3 dígitos para que dois retratos no mesmo milissegundo ordenem certo pelo nome.
        string arquivo;
        int seq = 0;
        do arquivo = Path.Combine(pasta, $"{r.Quando:yyyyMMdd-HHmmssfff}-{seq++:D3}.json");
        while (File.Exists(arquivo));
        File.WriteAllText(arquivo, Serializar(r));
        foreach (var velho in ArquivosDoHistorico().Skip(Limite)) File.Delete(velho);
    }

    public Retrato? Ultimo() => ArquivosDoHistorico().Select(Carregar).FirstOrDefault();

    public IReadOnlyList<Retrato> Historico() => ArquivosDoHistorico().Select(Carregar).ToList();

    public Retrato GarantirOriginal()
    {
        string arquivo = Path.Combine(pasta, NomeOriginal);
        if (File.Exists(arquivo)) return Carregar(arquivo);
        Directory.CreateDirectory(pasta);
        var r = Tirar(null);
        File.WriteAllText(arquivo, Serializar(r));
        return r;
    }

    /// Do mais recente para o mais antigo, sem o original.
    private IEnumerable<string> ArquivosDoHistorico() =>
        Directory.Exists(pasta)
            ? Directory.GetFiles(pasta, "*.json")
                .Where(f => !Path.GetFileName(f).Equals(NomeOriginal, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal)
            : [];

    // ---- serialização: Dado vira número (Dword), base64 (Binario), texto (Texto) ou null (ausente) ----

    private sealed record ValorJson(string Chave, string Nome, TipoValor Tipo, uint? Dword, string? Binario, string? Texto);
    private sealed record RetratoJson(DateTimeOffset Quando, string? PerfilAplicado, List<ValorJson> Valores);

    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

    private static string Serializar(Retrato r) => JsonSerializer.Serialize(new RetratoJson(
        r.Quando, r.PerfilAplicado,
        r.Valores.Select(v => new ValorJson(v.Chave, v.Nome, v.Tipo,
            v.Dado as uint?, v.Dado is byte[] b ? Convert.ToBase64String(b) : null, v.Dado as string)).ToList()), Opcoes);

    private static Retrato Carregar(string arquivo)
    {
        var j = JsonSerializer.Deserialize<RetratoJson>(File.ReadAllText(arquivo), Opcoes)
                ?? throw new InvalidDataException($"Retrato vazio: {arquivo}");
        return new Retrato(j.Quando, j.PerfilAplicado, j.Valores.Select(v => new ValorRegistro(v.Chave, v.Nome, v.Tipo,
            v.Tipo switch
            {
                TipoValor.Dword => v.Dword,
                TipoValor.Binario => v.Binario is null ? null : Convert.FromBase64String(v.Binario),
                _ => v.Texto,
            })).ToList());
    }
}
