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
            var resultado = aplicador.Aplicar(perfis.Carregar(t.Nome));
            int codigo = Relatar(resultado);
            Console.Write($"Manter este visual? (s/n) Volta ao anterior em {t.Segundos} s: ");
            var leitura = Task.Run(Console.ReadLine);
            bool manter = leitura.Wait(TimeSpan.FromSeconds(t.Segundos)) &&
                          leitura.Result?.Trim().Equals("s", StringComparison.OrdinalIgnoreCase) == true;
            if (manter) { Console.WriteLine("Mantido."); return codigo; }
            aplicador.Reverter(resultado.RetratoAnterior);
            Console.WriteLine();
            Console.WriteLine("Revertido ao estado anterior.");
            return 0;
        }

        case Comando.Desfazer:
        {
            var ultimo = retratos.Ultimo();
            if (ultimo is null) { Console.WriteLine("Não há mudança para desfazer."); return 0; }
            aplicador.Reverter(ultimo);
            Console.WriteLine($"Desfeito: voltou ao estado de {ultimo.Quando:dd/MM HH:mm:ss}.");
            return 0;
        }

        case Comando.Original:
            aplicador.Reverter(retratos.GarantirOriginal());
            Console.WriteLine("Voltou ao Windows original (como estava antes do primeiro uso do Vitral).");
            return 0;

        case Comando.Exportar e:
            Pacote.Exportar(perfis, e.Nomes, e.Arquivo, versao, Environment.OSVersion.Version.ToString());
            Console.WriteLine($"Exportado: {e.Arquivo}");
            return 0;

        case Comando.Importar i:
            foreach (var p in Pacote.Importar(i.Arquivo, perfis)) Console.WriteLine($"Importado: {p.Nome}");
            return 0;

        default:
            Console.WriteLine(Comando.TextoDeUso);
            return 1;
    }
}
catch (Exception ex) when (ex is PerfilInvalidoException or FileNotFoundException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
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
