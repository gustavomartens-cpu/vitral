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

    public EditorBarraViewModel EditorBarra { get; } = new();

    public EditorDocaViewModel EditorDoca { get; } = new();
    public EditorAparenciaViewModel EditorAparencia { get; } = new();
    public EditorFormasViewModel EditorFormas { get; } = new();

    public EditorBarraTarefasViewModel EditorBarraTarefas { get; }

    [ObservableProperty] private string? _selecionado;
    [ObservableProperty] private string? _perfilAtivo;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private ContagemViewModel? _contagem;

    /// O que está nos editores difere do que foi carregado ou salvo (spec da interface §3.3).
    [ObservableProperty] private bool _temAlteracoes;

    private string _carregado = "";
    private bool _carregando, _revertendo;
    private IDisposable? _relogioDaMensagem;

    /// <param name="programasBandeja">Programas que já apareceram perto do relógio (o App lê do registro); nulo = nenhum.</param>
    public PrincipalViewModel(ServicoVitral servico, RepositorioPerfis perfis, IDialogos dialogos, IRelogio relogio,
        Func<IReadOnlyList<Vitral.Core.Nativo.ProgramaBandeja>>? programasBandeja = null)
    {
        _servico = servico;
        _perfis = perfis;
        _dialogos = dialogos;
        _relogio = relogio;
        EditorBarraTarefas = new EditorBarraTarefasViewModel(programasBandeja ?? (() => []));
        foreach (var editor in new System.ComponentModel.INotifyPropertyChanged[] { Editor, EditorBarra, EditorBarraTarefas, EditorDoca, EditorAparencia, EditorFormas })
            editor.PropertyChanged += (_, _) => AtualizarAlteracoes();
        AtualizarLista();
        PerfilAtivo = servico.PerfilAtivo;
        // Abre no perfil ativo; sem ele (ou se sumiu), no primeiro; sem perfis, em nenhum (spec da interface §3.2).
        Selecionado = Perfis.FirstOrDefault(p => string.Equals(p, PerfilAtivo, StringComparison.OrdinalIgnoreCase)) ?? Perfis.FirstOrDefault();
    }

    /// O perfil montado pelos editores, como texto: comparar com o carregado diz se há alteração.
    private string Assinatura() => System.Text.Json.JsonSerializer.Serialize(new object?[]
    {
        Editor.ParaPerfil(), EditorBarra.ParaPerfil(), EditorBarraTarefas.ParaPerfil(), EditorDoca.ParaPerfil(),
        EditorAparencia.ParaPerfil(), EditorFormas.ParaPerfil(),
    });

    private void AtualizarAlteracoes()
    {
        if (_carregando) return;
        TemAlteracoes = Selecionado is not null && Assinatura() != _carregado;
    }

    /// O que está nos editores passa a ser o ponto de comparação (depois de carregar ou salvar).
    /// A janela está à vista. Escondida, a mensagem não começa a contar os 6 s: ninguém leu ainda.
    [ObservableProperty] private bool _janelaVisivel = true;

    /// "Sair" da bandeja com alteração pendente pergunta antes; false = o usuário desistiu de sair.
    public bool PodeSair() => ResolverPendencia();

    /// Antes de uma ação que troca o que o editor mostra (criar, importar, sair): com alteração pendente, pergunta
    /// Salvar, Descartar ou Cancelar. Devolve false para desistir da ação.
    private bool ResolverPendencia()
    {
        if (!TemAlteracoes || Selecionado is not { } perfil) return true;
        switch (_dialogos.PerguntarSalvar(perfil))
        {
            case RespostaPendente.Cancelar: return false;
            case RespostaPendente.Salvar: return SalvarEditor(perfil);
        }
        try { CarregarEditores(_perfis.Carregar(perfil)); }
        catch (Exception e) when (e is not OutOfMemoryException) { MarcarSemAlteracoes(); }
        return true;
    }

    private void MarcarSemAlteracoes()
    {
        _carregado = Assinatura();
        TemAlteracoes = false;
    }

    /// A mensagem de resultado some sozinha depois de 6 s (spec da interface §3.3).
    partial void OnMensagemChanged(string? value)
    {
        _relogioDaMensagem?.Dispose();
        _relogioDaMensagem = null;
        if (value is null) return;
        int segundos = 0;
        _relogioDaMensagem = _relogio.ACadaSegundo(() => { if (JanelaVisivel && ++segundos >= 6) Mensagem = null; });
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

    /// Casa as linhas com <see cref="Perfis"/> pelo nome: remove, move e insere pontualmente, e atualiza
    /// cor e ativo na própria linha. Nunca troca uma linha existente (a ListBox soltaria a seleção).
    private void SincronizarItens()
    {
        for (int i = ItensPerfis.Count - 1; i >= 0; i--)
            if (!Perfis.Contains(ItensPerfis[i].Nome, StringComparer.OrdinalIgnoreCase)) ItensPerfis.RemoveAt(i);
        for (int i = 0; i < Perfis.Count; i++)
        {
            string nome = Perfis[i];
            int existente = -1;
            for (int j = i; j < ItensPerfis.Count; j++)
                if (string.Equals(ItensPerfis[j].Nome, nome, StringComparison.OrdinalIgnoreCase)) { existente = j; break; }
            bool ativo = string.Equals(nome, PerfilAtivo, StringComparison.OrdinalIgnoreCase);
            if (existente < 0) { ItensPerfis.Insert(i, new ItemPerfil(nome, CorDe(nome), ativo)); continue; }
            if (existente != i) ItensPerfis.Move(existente, i);
            ItensPerfis[i].CorGema = CorDe(nome);
            ItensPerfis[i].Ativo = ativo;
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
            CarregarEditores(_perfis.Carregar(Selecionado));
            Mensagem = "Alterações descartadas.";
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _dialogos.Avisar(e.Message);
        }
    }

    private void CarregarEditores(Perfil? p)
    {
        _carregando = true;
        try
        {
            Editor.Carregar(p?.Nativo);
            EditorBarra.Carregar(p?.BarraTopo);
            EditorBarraTarefas.Carregar(p?.BarraTarefas);
            EditorDoca.Carregar(p?.BarraBaixo);
            EditorAparencia.Carregar(p?.Aparencia);
            EditorFormas.Carregar(p?.Formas);
        }
        finally { _carregando = false; }
        MarcarSemAlteracoes();
    }

    /// Perfis para o menu da bandeja, sem tocar na lista nem no editor da janela.
    public IReadOnlyList<string> NomesParaMenu() => _perfis.Nomes();

    partial void OnSelecionadoChanged(string? oldValue, string? newValue)
    {
        if (_revertendo) return;
        // Trocar com alteração pendente pergunta antes (spec da interface §3.6); Cancelar (ou salvar com erro) fica onde estava.
        if (oldValue is not null && TemAlteracoes)
        {
            var resposta = _dialogos.PerguntarSalvar(oldValue);
            if (resposta == RespostaPendente.Cancelar || (resposta == RespostaPendente.Salvar && !SalvarEditor(oldValue)))
            {
                Reverter(oldValue);
                return;
            }
        }
        if (newValue is null) { CarregarEditores(null); return; }
        try { CarregarEditores(_perfis.Carregar(newValue)); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // O editor ainda mostra o anterior: ficar no novo deixaria "Salvar" gravar o anterior por cima dele.
            Reverter(oldValue);
            if (oldValue is null) CarregarEditores(null);
            Mensagem = $"Não foi possível abrir \"{newValue}\": {e.Message}";
        }
    }

    /// Volta a seleção sem repetir a troca (nem a pergunta).
    private void Reverter(string? perfil)
    {
        _revertendo = true;
        try { Selecionado = perfil; }
        finally { _revertendo = false; }
    }

    [RelayCommand]
    private void Novo()
    {
        string? nome = _dialogos.PedirTexto("Nome do novo perfil:", "Novo perfil")?.Trim();
        if (string.IsNullOrEmpty(nome)) return;
        if (Perfis.Contains(nome, StringComparer.OrdinalIgnoreCase)) { _dialogos.Avisar($"Já existe um perfil \"{nome}\"."); return; }
        if (!ResolverPendencia()) return;
        bool ok = Executar(() => { _perfis.Salvar(new Perfil(PerfilJson.EsquemaSuportado, nome, null, new AjustesNativosPerfil(null, null, null, null, null, null, null, null))); return true; });
        if (ok) AtualizarLista(nome);
    }

    [RelayCommand]
    private void SalvarPerfil() => SalvarEditor();

    /// Salva o editor no perfil selecionado (ou no <paramref name="perfil"/> dado: o que está sendo deixado numa troca).
    /// Devolve false (e avisa) se algo estiver inválido ou falhar.
    private bool SalvarEditor(string? perfil = null)
    {
        perfil ??= Selecionado;
        if (perfil is null) { _dialogos.Avisar("Escolha ou crie um perfil primeiro."); return false; }
        var ajustes = Editor.ParaPerfil();
        var barra = EditorBarra.ParaPerfil();
        var barraTarefas = EditorBarraTarefas.ParaPerfil();
        var doca = EditorDoca.ParaPerfil();
        var aparencia = EditorAparencia.ParaPerfil();
        var formas = EditorFormas.ParaPerfil();
        if (ajustes.CorDestaque is { } cor)
        {
            try { Cor.DeHex(cor); }
            catch (FormatException e) { _dialogos.Avisar(e.Message); return false; }
        }
        string nome = perfil;
        bool ok = Executar(() =>
        {
            // Parte do perfil existente: seções que este editor não edita (ou de versões futuras) são preservadas.
            Perfil? existente = null;
            try { existente = _perfis.Carregar(nome); }
            catch (Exception e) when (e is not OutOfMemoryException) { /* perfil novo ou ilegível: começa do zero */ }
            var p = (existente ?? new Perfil(PerfilJson.EsquemaSuportado, nome, null, null)) with
            {
                Esquema = PerfilJson.EsquemaSuportado,
                BuildTestada = Environment.OSVersion.Version.ToString(),
                Nativo = ajustes,
                BarraTopo = barra,
                BarraTarefas = barraTarefas,
                BarraBaixo = doca,
                Aparencia = aparencia,
                Formas = formas,
            };
            _perfis.Salvar(p);
            return true;
        });
        if (ok) { SincronizarItens(); MarcarSemAlteracoes(); } // a gema acompanha a cor salva
        return ok;
    }

    [RelayCommand]
    private void Testar()
    {
        if (Contagem is not null) { Mensagem = AvisoTesteEmCurso; return; }
        string? perfil = Selecionado; // guardado antes de salvar: salvar mexe na lista
        if (!SalvarEditor()) return;
        IniciarTeste(perfil!, SegundosDoTeste);
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
        string? selecionado = Selecionado; // guardado antes de salvar: salvar mexe na lista
        if (!SalvarEditor()) return;
        string perfil = selecionado!;
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
        if (arquivo is null || !ResolverPendencia()) return;
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
        string? aberto = Selecionado;
        AtualizarLista(importados.FirstOrDefault()?.Nome);
        // Substituiu o perfil que continua aberto: o editor mostra o importado, não o que estava no disco antes.
        if (aberto is not null && Selecionado == aberto && importados.Any(p => string.Equals(p.Nome, aberto, StringComparison.OrdinalIgnoreCase)))
            CarregarEditores(_perfis.Carregar(aberto));
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
