using System.Text.Json;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;

namespace Vitral.Core.Retratos;

/// Tira, guarda e lista retratos em uma pasta. Mantém os 10 mais recentes;
/// o original.json (estado do Windows antes do primeiro uso) nunca é apagado.
/// <paramref name="barraAtual"/> informa o estado da barra do topo para o retrato; sem ele, o retrato não mexe na barra.
public sealed class ServicoRetratos(IRegistro registro, string pasta, Func<BarraTopoPerfil>? barraAtual = null)
{
    private const int Limite = 10;
    private const string NomeOriginal = "original.json";

    public Retrato Tirar(string? perfil) => new(
        DateTimeOffset.Now,
        perfil,
        AjustesNativos.Controlados.Concat(BarraDeTarefas.Controlados).Select(c => registro.Ler(c.Chave, c.Nome, c.Tipo)).ToList(),
        barraAtual?.Invoke());

    /// Grava o retrato e devolve o nome do arquivo (para guardar a referência, por exemplo num teste em andamento).
    public string Salvar(Retrato r)
    {
        Directory.CreateDirectory(pasta);
        // Sequencial com 3 dígitos para que dois retratos no mesmo milissegundo ordenem certo pelo nome.
        string arquivo;
        int seq = 0;
        do arquivo = Path.Combine(pasta, $"{r.Quando:yyyyMMdd-HHmmssfff}-{seq++:D3}.json");
        while (File.Exists(arquivo));
        File.WriteAllText(arquivo, Serializar(r));
        foreach (var velho in ArquivosDoHistorico().Skip(Limite)) File.Delete(velho);
        return Path.GetFileName(arquivo);
    }

    /// Carrega um retrato pelo nome do arquivo; nulo se ele não existir mais.
    public Retrato? Carregar(string nomeArquivo)
    {
        string arquivo = Path.Combine(pasta, Path.GetFileName(nomeArquivo));
        return File.Exists(arquivo) ? LerArquivo(arquivo) : null;
    }

    public Retrato? Ultimo() => ArquivosDoHistorico().Select(LerArquivo).FirstOrDefault();

    public IReadOnlyList<Retrato> Historico() => ArquivosDoHistorico().Select(LerArquivo).ToList();

    public Retrato GarantirOriginal()
    {
        string arquivo = Path.Combine(pasta, NomeOriginal);
        // O original de antes da barra existir não tem barraTopo: o Windows original nunca teve barra, então vale desligada.
        if (File.Exists(arquivo)) { var o = LerArquivo(arquivo); return o.BarraTopo is null ? o with { BarraTopo = BarraTopoPerfil.Desligada } : o; }
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
    private sealed record RetratoJson(DateTimeOffset Quando, string? PerfilAplicado, List<ValorJson> Valores, BarraTopoPerfil? BarraTopo = null);

    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = true };

    private static string Serializar(Retrato r) => JsonSerializer.Serialize(new RetratoJson(
        r.Quando, r.PerfilAplicado,
        r.Valores.Select(v => new ValorJson(v.Chave, v.Nome, v.Tipo,
            v.Dado as uint?, v.Dado is byte[] b ? Convert.ToBase64String(b) : null, v.Dado as string)).ToList(), r.BarraTopo), Opcoes);

    private static Retrato LerArquivo(string arquivo)
    {
        var j = JsonSerializer.Deserialize<RetratoJson>(File.ReadAllText(arquivo), Opcoes)
                ?? throw new InvalidDataException($"Retrato vazio: {arquivo}");
        return new Retrato(j.Quando, j.PerfilAplicado, j.Valores.Select(v => new ValorRegistro(v.Chave, v.Nome, v.Tipo,
            v.Tipo switch
            {
                TipoValor.Dword => v.Dword,
                TipoValor.Binario => v.Binario is null ? null : Convert.FromBase64String(v.Binario),
                _ => v.Texto,
            })).ToList(), j.BarraTopo);
    }
}
