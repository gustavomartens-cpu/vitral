using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vitral.Core.Aplicacao;
using Vitral.Core.Cores;
using Vitral.Core.Perfis;
using Vitral.Core.Registro;
using Vitral.Core.Servicos;

namespace Vitral.Ui;

/// Janela principal: lista de perfis, editor e ações. Testar e Aplicar salvam antes o que está no editor.
public sealed partial class PrincipalViewModel : ObservableObject
{
    public const int SegundosDoTeste = 20;
    private const string FiltroVitral = "Perfis do Vitral (*.vitral)|*.vitral";

    private readonly ServicoVitral _servico;
    private readonly RepositorioPerfis _perfis;
    private readonly IDialogos _dialogos;
    private readonly IRelogio _relogio;

    public ObservableCollection<string> Perfis { get; } = [];
    public EditorNativoViewModel Editor { get; } = new();

    [ObservableProperty] private string? _selecionado;
    [ObservableProperty] private string? _perfilAtivo;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private ContagemViewModel? _contagem;

    public PrincipalViewModel(ServicoVitral servico, RepositorioPerfis perfis, IDialogos dialogos, IRelogio relogio)
    {
        _servico = servico;
        _perfis = perfis;
        _dialogos = dialogos;
        _relogio = relogio;
        AtualizarLista();
        PerfilAtivo = servico.PerfilAtivo;
    }

    public void AtualizarLista(string? selecionar = null)
    {
        Perfis.Clear();
        foreach (var n in _perfis.Nomes()) Perfis.Add(n);
        if (selecionar is not null) Selecionado = selecionar;
    }

    partial void OnSelecionadoChanged(string? value)
    {
        if (value is null) { Editor.Carregar(null); return; }
        try { Editor.Carregar(_perfis.Carregar(value).Nativo); }
        catch (Exception e) when (e is PerfilInvalidoException or FileNotFoundException) { Mensagem = e.Message; }
    }

    [RelayCommand]
    private void Novo()
    {
        string? nome = _dialogos.PedirTexto("Nome do novo perfil:", "Novo perfil")?.Trim();
        if (string.IsNullOrEmpty(nome)) return;
        if (Perfis.Contains(nome, StringComparer.OrdinalIgnoreCase)) { _dialogos.Avisar($"Já existe um perfil \"{nome}\"."); return; }
        _perfis.Salvar(new Perfil(PerfilJson.EsquemaSuportado, nome, null, new AjustesNativosPerfil(null, null, null, null, null, null, null, null)));
        AtualizarLista(nome);
    }

    [RelayCommand]
    private void SalvarPerfil() => SalvarEditor();

    /// Salva o editor no perfil selecionado. Devolve false (e avisa) se algo estiver inválido.
    private bool SalvarEditor()
    {
        if (Selecionado is null) { _dialogos.Avisar("Escolha ou crie um perfil primeiro."); return false; }
        var ajustes = Editor.ParaPerfil();
        if (ajustes.CorDestaque is { } cor)
        {
            try { Cor.DeHex(cor); }
            catch (FormatException e) { _dialogos.Avisar(e.Message); return false; }
        }
        _perfis.Salvar(new Perfil(PerfilJson.EsquemaSuportado, Selecionado, Environment.OSVersion.Version.ToString(), ajustes));
        return true;
    }

    [RelayCommand]
    private void Testar()
    {
        if (Contagem is not null || !SalvarEditor()) return;
        string perfil = Selecionado!;
        var resultado = Executar(() => _servico.IniciarTeste(perfil));
        if (resultado is null) return;
        Mensagem = Descrever(resultado, $"Testando \"{perfil}\".");
        Contagem = new ContagemViewModel(SegundosDoTeste, _relogio,
            manter: () =>
            {
                _servico.ConfirmarTeste();
                PerfilAtivo = _servico.PerfilAtivo;
                Mensagem = $"\"{perfil}\" mantido.";
                Contagem = null;
            },
            voltar: () =>
            {
                var naoVoltou = Executar(() => _servico.CancelarTeste());
                if (naoVoltou is not null) Mensagem = DescreverVolta(naoVoltou, "Voltou ao anterior.");
                Contagem = null;
            });
    }

    [RelayCommand]
    private void Aplicar()
    {
        if (Contagem is not null || !SalvarEditor()) return;
        string perfil = Selecionado!;
        var resultado = Executar(() => _servico.Aplicar(perfil));
        if (resultado is null) return;
        PerfilAtivo = _servico.PerfilAtivo;
        Mensagem = Descrever(resultado, $"\"{perfil}\" aplicado.");
    }

    [RelayCommand]
    private void Desfazer()
    {
        var naoVoltou = Executar(() => _servico.Desfazer());
        if (naoVoltou is not null) Mensagem = DescreverVolta(naoVoltou, "Desfeito: voltou ao estado anterior.");
    }

    [RelayCommand]
    private void Original()
    {
        if (!_dialogos.Confirmar("Voltar o Windows ao estado de antes do primeiro uso do Vitral? (Dá para desfazer depois.)")) return;
        var naoVoltou = Executar(() => _servico.Original());
        if (naoVoltou is not null) Mensagem = DescreverVolta(naoVoltou, "Voltou ao Windows original.");
    }

    [RelayCommand]
    private void Exportar()
    {
        if (Selecionado is null) { _dialogos.Avisar("Escolha um perfil para exportar."); return; }
        string? arquivo = _dialogos.EscolherArquivoParaSalvar(FiltroVitral, $"{RepositorioPerfis.NomeDePasta(Selecionado)}.vitral");
        if (arquivo is null) return;
        string versao = typeof(PrincipalViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        if (Executar(() => { Pacote.Exportar(_perfis, [Selecionado], arquivo, versao, Environment.OSVersion.Version.ToString()); return true; }))
            Mensagem = $"Exportado: {arquivo}";
    }

    [RelayCommand]
    private void Importar()
    {
        string? arquivo = _dialogos.EscolherArquivoParaAbrir(FiltroVitral);
        if (arquivo is null) return;
        IReadOnlyList<Perfil>? importados;
        try
        {
            importados = Pacote.Importar(arquivo, _perfis);
        }
        catch (PerfilJaExisteException e)
        {
            if (!_dialogos.Confirmar($"{e.Message.Split(" Nada foi")[0]} Substituir?")) { Mensagem = "Importação cancelada."; return; }
            importados = Executar(() => Pacote.Importar(arquivo, _perfis, substituir: true));
        }
        catch (PerfilInvalidoException e)
        {
            _dialogos.Avisar(e.Message);
            return;
        }
        if (importados is null) return;
        AtualizarLista(importados.FirstOrDefault()?.Nome);
        Mensagem = $"Importado: {string.Join(", ", importados.Select(p => p.Nome))}";
    }

    /// Roda uma ação e transforma erro esperado em aviso (nunca derruba a janela).
    private T? Executar<T>(Func<T> acao)
    {
        try { return acao(); }
        catch (Exception e) when (e is PerfilInvalidoException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            _dialogos.Avisar(e.Message);
            return default;
        }
    }

    private static string Descrever(Resultado r, string ok) =>
        r.Ok ? ok : $"{ok} Em parte: o Windows não deixou aplicar {string.Join(", ", r.NaoAplicados.Select(n => n.Valor.Nome))}.";

    private static string DescreverVolta(IReadOnlyList<(ValorRegistro Valor, string Motivo)> naoVoltou, string ok) =>
        naoVoltou.Count == 0 ? ok : $"{ok} Em parte: o Windows não deixou restaurar {string.Join(", ", naoVoltou.Select(n => n.Valor.Nome))}.";
}
