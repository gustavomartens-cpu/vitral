using Vitral.Core.Doca;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Vitral.Ui.Doca;

/// Um botão da doca. Atualizado no lugar (a coleção nunca troca o objeto de um app que continua na doca).
public sealed partial class BotaoAppViewModel(string chave) : ObservableObject
{
    public string Chave { get; } = chave;

    [ObservableProperty] private string _nome = "";
    [ObservableProperty] private object? _icone;
    [ObservableProperty] private bool _aberto;
    [ObservableProperty] private bool _ativo;
    [ObservableProperty] private bool _pedindoAtencao;
    [ObservableProperty] private bool _atalhoExiste = true;
    [ObservableProperty] private Fixado? _fixado;
    [ObservableProperty] private IReadOnlyList<JanelaApp> _janelas = [];
}

/// A doca (spec da doca §5): Iniciar, pesquisa e um botão por app (fixados e abertos).
public sealed partial class DocaViewModel : ObservableObject, IDisposable
{
    private readonly IFonteJanelas _janelas;
    private readonly IFonteFixados _fixados;
    private readonly IAtalhosDoca _atalhos;
    private readonly IDialogos _dialogos;

    public ObservableCollection<BotaoAppViewModel> Botoes { get; } = [];

    /// Clique num app com várias janelas: a tela mostra as miniaturas para escolher.
    public event Action<BotaoAppViewModel>? PediuEscolha;

    public DocaViewModel(IFonteJanelas janelas, IFonteFixados fixados, IAtalhosDoca atalhos, IDialogos dialogos)
    {
        _janelas = janelas;
        _fixados = fixados;
        _atalhos = atalhos;
        _dialogos = dialogos;
        _janelas.Mudou += Atualizar;
        Atualizar();
    }

    /// Sincroniza por Chave: remove, move e insere pontualmente; o resto muda no próprio botão.
    public void Atualizar()
    {
        var grupos = GrupoApps.Montar(_fixados.Lista, _janelas.Janelas);
        var ativa = _janelas.Ativa;
        for (int i = Botoes.Count - 1; i >= 0; i--)
            if (!grupos.Any(g => g.Chave == Botoes[i].Chave)) Botoes.RemoveAt(i);
        for (int i = 0; i < grupos.Count; i++)
        {
            var g = grupos[i];
            int existente = -1;
            for (int j = i; j < Botoes.Count; j++)
                if (Botoes[j].Chave == g.Chave) { existente = j; break; }
            if (existente < 0) Botoes.Insert(i, new BotaoAppViewModel(g.Chave));
            else if (existente != i) Botoes.Move(existente, i);
            var b = Botoes[i];
            b.Nome = g.Nome;
            b.Icone = g.Janelas.FirstOrDefault(j => j.Icone is not null)?.Icone ?? b.Icone ?? (g.Fixado is null ? null : _fixados.Icone(g.Fixado));
            b.Aberto = g.Janelas.Count > 0;
            b.Ativo = g.Janelas.Any(j => j.Handle == ativa);
            b.PedindoAtencao = g.Janelas.Any(j => j.PedindoAtencao);
            b.Fixado = g.Fixado;
            b.AtalhoExiste = g.Fixado is null || _fixados.Existe(g.Fixado);
            b.Janelas = g.Janelas;
        }
    }

    [RelayCommand] private void Iniciar() => _atalhos.Iniciar();
    [RelayCommand] private void Pesquisar() => _atalhos.Pesquisar();

    [RelayCommand]
    private void Clicar(BotaoAppViewModel? botao)
    {
        if (botao is null) return;
        var grupo = new GrupoApp(botao.Chave, botao.Nome, botao.Fixado, botao.Janelas);
        var acao = GrupoApps.Decidir(grupo, _janelas.Ativa);
        switch (acao.Tipo)
        {
            case TipoAcao.Abrir when botao.Fixado is { } f:
                if (!botao.AtalhoExiste) { _dialogos.Avisar("Atalho não encontrado"); return; }
                _atalhos.Abrir(f);
                break;
            case TipoAcao.Ativar: _janelas.Ativar(acao.Janela); break;
            case TipoAcao.Minimizar: _janelas.Minimizar(acao.Janela); break;
            case TipoAcao.Escolher: PediuEscolha?.Invoke(botao); break;
        }
    }

    /// Fixa um app aberto: pelo executável; app da loja (sem executável próprio) pelo AppId no shell:AppsFolder.
    [RelayCommand]
    private void Fixar(BotaoAppViewModel? botao)
    {
        if (botao is null || botao.Fixado is not null || botao.Janelas.Count == 0) return;
        var j = botao.Janelas[0];
        bool daLoja = j.AppId is { } id && !id.StartsWith("janela:", StringComparison.Ordinal)
            && (j.Executavel.Length == 0 || j.Executavel.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase));
        var novo = daLoja
            ? new Fixado($@"shell:AppsFolder\{j.AppId}", j.AppId, botao.Nome)
            : new Fixado(j.Executavel, j.AppId?.StartsWith("janela:", StringComparison.Ordinal) == true ? null : j.AppId, botao.Nome, j.Executavel);
        Salvar([.. _fixados.Lista, novo]);
    }

    [RelayCommand]
    private void Desafixar(BotaoAppViewModel? botao)
    {
        if (botao?.Fixado is not { } f) return;
        Salvar(_fixados.Lista.Where(x => x != f).ToList());
    }

    /// Arrastar na doca: só entre fixados (os abertos não fixados seguem a ordem de chegada).
    public void Mover(int de, int para)
    {
        var lista = _fixados.Lista.ToList();
        if (de == para || de < 0 || para < 0 || de >= lista.Count || para >= lista.Count) return;
        var f = lista[de];
        lista.RemoveAt(de);
        lista.Insert(para, f);
        Salvar(lista);
    }

    private void Salvar(IReadOnlyList<Fixado> lista)
    {
        _fixados.Salvar(lista);
        Atualizar();
    }

    public void Dispose() => _janelas.Mudou -= Atualizar;
}
