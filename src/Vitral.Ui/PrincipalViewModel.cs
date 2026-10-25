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
/// Durante a contagem de um teste, só Manter/Voltar valem: as outras mudanças esperam (evita misturar estados).
public sealed partial class PrincipalViewModel : ObservableObject
{
    public const int SegundosDoTeste = 20;
    public const int SegundosDaBandeja = 12;
    private const string FiltroVitral = "Perfis do Vitral (*.vitral)|*.vitral";
    private const string AvisoTesteEmCurso = "Há um teste em andamento: clique em Manter ou Voltar primeiro.";

    private readonly ServicoVitral _servico;
    private readonly RepositorioPerfis _perfis;
    private readonly IDialogos _dialogos;
    private readonly IRelogio _relogio;

    public ObservableCollection<string> Perfis { get; } = [];

    /// Linhas da lista com gema e marca de ativo; sincronizada com <see cref="Perfis"/> e <see cref="PerfilAtivo"/>.
    public ObservableCollection<ItemPerfil> ItensPerfis { get; } = [];

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

    /// Sincroniza a lista com o disco sem "Reset": um Reset faz a ListBox soltar a seleção
    /// e o editor perderia o que não foi salvo.
    public void AtualizarLista(string? selecionar = null)
    {
        var nomes = _perfis.Nomes();
        for (int i = Perfis.Count - 1; i >= 0; i--)
            if (!nomes.Contains(Perfis[i], StringComparer.OrdinalIgnoreCase)) Perfis.RemoveAt(i);
        for (int i = 0; i < nomes.Count; i++)
        {
            if (i < Perfis.Count && string.Equals(Perfis[i], nomes[i], StringComparison.OrdinalIgnoreCase)) continue;
            int existente = Perfis.IndexOf(nomes[i]);
            if (existente >= 0) Perfis.Move(existente, i);
            else Perfis.Insert(i, nomes[i]);
        }
        SincronizarItens();
        if (selecionar is not null && selecionar != Selecionado) Selecionado = selecionar;
    }

    partial void OnPerfilAtivoChanged(string? value) => SincronizarItens();

    /// Atualiza as linhas sem Reset: troca só as que mudaram (cor ou ativo), insere e remove pontualmente.
    private void SincronizarItens()
    {
        var novos = Perfis.Select(n => new ItemPerfil(n, CorDe(n), string.Equals(n, PerfilAtivo, StringComparison.OrdinalIgnoreCase))).ToList();
        while (ItensPerfis.Count > novos.Count) ItensPerfis.RemoveAt(ItensPerfis.Count - 1);
        for (int i = 0; i < novos.Count; i++)
        {
            if (i >= ItensPerfis.Count) ItensPerfis.Add(novos[i]);
            else if (ItensPerfis[i] != novos[i]) ItensPerfis[i] = novos[i];
        }
    }

    private string CorDe(string perfil)
    {
        try { return _perfis.Carregar(perfil).Nativo?.CorDestaque ?? ItemPerfil.CorPadrao; }
        catch (Exception e) when (e is not OutOfMemoryException) { return ItemPerfil.CorPadrao; }
    }

    [RelayCommand]
    private void Descartar()
    {
        if (Selecionado is null) { _dialogos.Avisar("Escolha um perfil primeiro."); return; }
        try
        {
            Editor.Carregar(_perfis.Carregar(Selecionado).Nativo);
            Mensagem = "Alterações descartadas.";
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _dialogos.Avisar(e.Message);
        }
    }

    /// Perfis para o menu da bandeja, sem tocar na lista nem no editor da janela.
    public IReadOnlyList<string> NomesParaMenu() => _perfis.Nomes();

    partial void OnSelecionadoChanged(string? value)
    {
        if (value is null) { Editor.Carregar(null); return; }
        try { Editor.Carregar(_perfis.Carregar(value).Nativo); }
        catch (Exception e) when (e is not OutOfMemoryException) { Mensagem = e.Message; }
    }

    [RelayCommand]
    private void Novo()
    {
        string? nome = _dialogos.PedirTexto("Nome do novo perfil:", "Novo perfil")?.Trim();
        if (string.IsNullOrEmpty(nome)) return;
        if (Perfis.Contains(nome, StringComparer.OrdinalIgnoreCase)) { _dialogos.Avisar($"Já existe um perfil \"{nome}\"."); return; }
        bool ok = Executar(() => { _perfis.Salvar(new Perfil(PerfilJson.EsquemaSuportado, nome, null, new AjustesNativosPerfil(null, null, null, null, null, null, null, null))); return true; });
        if (ok) AtualizarLista(nome);
    }

    [RelayCommand]
    private void SalvarPerfil() => SalvarEditor();

    /// Salva o editor no perfil selecionado. Devolve false (e avisa) se algo estiver inválido ou falhar.
    private bool SalvarEditor()
    {
        if (Selecionado is null) { _dialogos.Avisar("Escolha ou crie um perfil primeiro."); return false; }
        var ajustes = Editor.ParaPerfil();
        if (ajustes.CorDestaque is { } cor)
        {
            try { Cor.DeHex(cor); }
            catch (FormatException e) { _dialogos.Avisar(e.Message); return false; }
        }
        string nome = Selecionado;
        bool ok = Executar(() => { _perfis.Salvar(new Perfil(PerfilJson.EsquemaSuportado, nome, Environment.OSVersion.Version.ToString(), ajustes)); return true; });
        if (ok) SincronizarItens(); // a gema acompanha a cor salva
        return ok;
    }

    [RelayCommand]
    private void Testar()
    {
        if (Contagem is not null) { Mensagem = AvisoTesteEmCurso; return; }
        if (!SalvarEditor()) return;
        IniciarTeste(Selecionado!, SegundosDoTeste);
    }

    /// Troca rápida pela bandeja: testa o perfil salvo (não o editor) com contagem de 12 s.
    /// Devolve o que mostrar numa notificação (aplicação parcial ou teste já em curso), ou null.
    public string? TrocarPelaBandeja(string perfil)
    {
        if (Contagem is not null) return AvisoTesteEmCurso;
        var resultado = IniciarTeste(perfil, SegundosDaBandeja);
        return resultado is { Ok: false } ? Mensagem : null;
    }

    private Resultado? IniciarTeste(string perfil, int segundos)
    {
        var resultado = Executar(() => _servico.IniciarTeste(perfil));
        if (resultado is null) return null;
        Mensagem = Descrever(resultado, $"Testando \"{perfil}\".");
        Contagem = new ContagemViewModel(segundos, _relogio,
            manter: () =>
            {
                if (Executar(() => { _servico.ConfirmarTeste(); return true; }))
                {
                    PerfilAtivo = _servico.PerfilAtivo;
                    Mensagem = $"\"{perfil}\" mantido.";
                }
                Contagem = null;
            },
            voltar: () =>
            {
                var naoVoltou = Executar(() => _servico.CancelarTeste());
                if (naoVoltou is not null) Mensagem = DescreverVolta(naoVoltou, "Voltou ao anterior.");
                Contagem = null;
            });
        return resultado;
    }

    [RelayCommand]
    private void Aplicar()
    {
        if (Contagem is not null) { Mensagem = AvisoTesteEmCurso; return; }
        if (!SalvarEditor()) return;
        string perfil = Selecionado!;
        var resultado = Executar(() => _servico.Aplicar(perfil));
        if (resultado is null) return;
        PerfilAtivo = _servico.PerfilAtivo;
        Mensagem = Descrever(resultado, $"\"{perfil}\" aplicado.");
    }

    [RelayCommand]
    private void Desfazer()
    {
        if (Contagem is not null) { Mensagem = AvisoTesteEmCurso; return; }
        var naoVoltou = Executar(() => _servico.Desfazer());
        if (naoVoltou is not null) Mensagem = DescreverVolta(naoVoltou, "Desfeito: voltou ao estado anterior.");
    }

    [RelayCommand]
    private void Original()
    {
        if (Contagem is not null) { Mensagem = AvisoTesteEmCurso; return; }
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
        string nome = Selecionado;
        string versao = typeof(PrincipalViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        if (Executar(() => { Pacote.Exportar(_perfis, [nome], arquivo, versao, Environment.OSVersion.Version.ToString()); return true; }))
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
            string nomes = string.Join(", ", e.Nomes.Select(n => $"\"{n}\""));
            if (!_dialogos.Confirmar($"O perfil {nomes} já existe neste computador. Substituir?")) { Mensagem = "Importação cancelada."; return; }
            importados = Executar(() => Pacote.Importar(arquivo, _perfis, substituir: true));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _dialogos.Avisar(e.Message);
            return;
        }
        if (importados is null) return;
        AtualizarLista(importados.FirstOrDefault()?.Nome);
        Mensagem = $"Importado: {string.Join(", ", importados.Select(p => p.Nome))}";
    }

    /// Roda uma ação e transforma qualquer erro em aviso: a janela nunca cai por causa de arquivo ou registro.
    private T? Executar<T>(Func<T> acao)
    {
        try { return acao(); }
        catch (Exception e) when (e is not OutOfMemoryException)
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
