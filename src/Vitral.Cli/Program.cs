using Vitral.Cli;
using Vitral.Core.Aplicacao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Retratos;
using Vitral.Core.Windows;

// Códigos de saída: 0 ok; 1 erro de uso, perfil inválido ou inexistente; 2 aplicado com itens não gravados.
var registro = new RegistroWindows();
var retratos = new ServicoRetratos(registro, Pastas.Retratos);
var aplicador = new Aplicador(registro, retratos, new NotificadorWindows());
var perfis = new RepositorioPerfis(Pastas.Perfis);
string versao = typeof(Comando).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

try
{
    switch (Comando.Interpretar(args))
    {
        case Comando.Retrato:
            foreach (var v in retratos.Tirar(null).Valores)
                Console.WriteLine($@"HKCU\{v.Chave}\{v.Nome} = {Formatar(v.Dado)}");
            return 0;

        case Comando.SalvarAtual s:
            perfis.Salvar(new Perfil(PerfilJson.EsquemaSuportado, s.Nome, Environment.OSVersion.Version.ToString(), AjustesNativos.LerAtual(registro)));
            Console.WriteLine($"Perfil \"{s.Nome}\" salvo em {perfis.Pasta}.");
            return 0;

        case Comando.Aplicar a:
            return Relatar(aplicador.Aplicar(perfis.Carregar(a.Nome)));

        case Comando.Testar t:
        {
            var perfil = perfis.Carregar(t.Nome);
            using var cancelar = new CancellationTokenSource();
            ConsoleCancelEventHandler aoCancelar = (_, e) => { e.Cancel = true; cancelar.Cancel(); }; // Ctrl+C = "não manter"
            Console.CancelKeyPress += aoCancelar;
            try
            {
                var resultado = aplicador.Aplicar(perfil);
                int codigo = Relatar(resultado);
                Console.Write($"Manter este visual? (s/n, Ctrl+C = não) Volta ao anterior em {t.Segundos} s: ");
                if (Confirmacao.Esperar(Console.ReadLine, TimeSpan.FromSeconds(t.Segundos), cancelar.Token))
                {
                    Console.WriteLine("Mantido. Para voltar depois: vitral desfazer");
                    return codigo;
                }
                Console.WriteLine();
                return RelatarVolta(aplicador.Reverter(resultado.RetratoAnterior), "Revertido ao estado anterior.");
            }
            finally
            {
                Console.CancelKeyPress -= aoCancelar;
            }
        }

        case Comando.Desfazer:
        {
            var ultimo = retratos.Ultimo();
            if (ultimo is null) { Console.WriteLine("Não há mudança para desfazer."); return 0; }
            return RelatarVolta(aplicador.Restaurar(ultimo, "(antes de desfazer)"),
                $"Desfeito: voltou ao estado de {ultimo.Quando:dd/MM HH:mm:ss}. Rode de novo para refazer.");
        }

        case Comando.Original:
            return RelatarVolta(aplicador.Restaurar(retratos.GarantirOriginal(), "(antes de original)"),
                "Voltou ao Windows original (como estava antes do primeiro uso do Vitral). Para voltar: vitral desfazer");

        case Comando.Exportar e:
            Pacote.Exportar(perfis, e.Nomes, e.Arquivo, versao, Environment.OSVersion.Version.ToString());
            Console.WriteLine($"Exportado: {e.Arquivo}");
            return 0;

        case Comando.Importar i:
            foreach (var p in Pacote.Importar(i.Arquivo, perfis, i.Substituir)) Console.WriteLine($"Importado: {p.Nome}");
            return 0;

        default:
            Console.WriteLine(Comando.TextoDeUso);
            return 1;
    }
}
catch (Exception ex) when (ex is PerfilInvalidoException or FileNotFoundException or DirectoryNotFoundException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Erro: {ex.Message}");
    Console.Error.WriteLine($"Detalhes técnicos em {Pastas.Logs}.");
    try
    {
        Directory.CreateDirectory(Pastas.Logs);
        File.AppendAllText(Path.Combine(Pastas.Logs, "cli-erros.log"), $"{DateTimeOffset.Now:O} {ex}{Environment.NewLine}");
    }
    catch { /* sem log, a mensagem acima já foi mostrada */ }
    return 1;
}

static int RelatarVolta(IReadOnlyList<(Vitral.Core.Registro.ValorRegistro Valor, string Motivo)> naoVoltou, string mensagemOk)
{
    if (naoVoltou.Count == 0) { Console.WriteLine(mensagemOk); return 0; }
    Console.WriteLine("Voltou em parte. O Windows não deixou restaurar:");
    foreach (var (v, motivo) in naoVoltou) Console.WriteLine($"  {v.Nome} ({motivo})");
    return 2;
}

static int Relatar(Resultado r)
{
    Console.WriteLine($"Aplicados: {r.Aplicados.Count} valores.");
    foreach (var (v, motivo) in r.NaoAplicados) Console.WriteLine($"NÃO aplicado: {v.Nome} ({motivo})");
    return r.Ok ? 0 : 2;
}

static string Formatar(object? dado) => dado switch
{
    null => "(ausente)",
    uint u => $"0x{u:X8} ({u})",
    byte[] b => Convert.ToHexString(b),
    _ => dado.ToString() ?? "",
};
