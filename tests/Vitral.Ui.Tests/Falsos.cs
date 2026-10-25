using Vitral.Core.Aplicacao;
using Vitral.Core.Configuracao;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Retratos;
using Vitral.Core.Servicos;

namespace Vitral.Ui.Tests;

internal sealed class RelogioFalso : IRelogio
{
    private Action? _tique;
    public bool Liberado { get; private set; }

    public IDisposable ACadaSegundo(Action tique)
    {
        _tique = tique;
        Liberado = false;
        return new Liberador(this);
    }

    public void Tique(int vezes = 1)
    {
        for (int i = 0; i < vezes; i++) _tique?.Invoke();
    }

    private sealed class Liberador(RelogioFalso r) : IDisposable
    {
        public void Dispose() { r.Liberado = true; r._tique = null; }
    }
}

internal sealed class DialogosFalsos : IDialogos
{
    public string? ArquivoParaAbrir { get; set; }
    public string? ArquivoParaSalvar { get; set; }
    public bool RespostaConfirmar { get; set; }
    public string? RespostaTexto { get; set; }
    public List<string> Avisos { get; } = [];
    public List<string> Perguntas { get; } = [];

    public string? EscolherArquivoParaAbrir(string filtro) => ArquivoParaAbrir;
    public string? EscolherArquivoParaSalvar(string filtro, string nomeSugerido) => ArquivoParaSalvar;
    public bool Confirmar(string pergunta) { Perguntas.Add(pergunta); return RespostaConfirmar; }
    public void Avisar(string mensagem) => Avisos.Add(mensagem);
    public string? PedirTexto(string pergunta, string sugestao) => RespostaTexto;
}

/// Monta o núcleo inteiro sobre um registro em memória e pastas temporárias.
internal sealed class Ambiente : IDisposable
{
    public const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    public string Raiz { get; } = Path.Combine(Path.GetTempPath(), "vitral-ui-" + Guid.NewGuid().ToString("N"));
    public RegistroEmMemoria Registro { get; } = new();
    public RepositorioPerfis Perfis { get; }
    public ServicoConfig Config { get; }
    public ServicoVitral Servico { get; }

    public Ambiente()
    {
        Perfis = new RepositorioPerfis(Path.Combine(Raiz, "perfis"));
        Config = new ServicoConfig(Path.Combine(Raiz, "config.json"));
        var retratos = new ServicoRetratos(Registro, Path.Combine(Raiz, "retratos"));
        Servico = new ServicoVitral(new Aplicador(Registro, retratos, new NotificadorNulo()), retratos, Perfis, Config);
        Registro.Gravar(new ValorRegistro(Personalize, "AppsUseLightTheme", TipoValor.Dword, 0u));
    }

    public uint? Apps() => Registro.Ler(Personalize, "AppsUseLightTheme", TipoValor.Dword).Dado as uint?;

    public void Dispose()
    {
        if (Directory.Exists(Raiz)) Directory.Delete(Raiz, recursive: true);
    }

    private sealed class NotificadorNulo : INotificador
    {
        public void AvisarMudancaDeTema() { }
        public void AvisarMudancaDaBarra() { }
        public void AvisarOcultarAutomatico(bool ligado) { }
    }
}
