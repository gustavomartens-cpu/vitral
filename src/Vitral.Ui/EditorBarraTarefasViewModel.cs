using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vitral.Core.Nativo;
using Vitral.Core.Perfis;

namespace Vitral.Ui;

/// Um programa na lista de ícones da aba: marcado = à mostra perto do relógio.
public sealed partial class ItemIcone(string executavel, string nome, bool encontrado) : ObservableObject
{
    public const string NaoEncontrado = " (não encontrado aqui)";

    public string Executavel { get; } = executavel;
    public string Nome { get; } = encontrado ? nome : nome + NaoEncontrado;

    [ObservableProperty] private bool _visivel;
}

/// Editor da seção barraTarefas (spec da barra de tarefas §6). "Não mexer" vira null; tudo "Não mexer" = seção nula.
/// Os ícones listam os programas deste PC mais os do perfil que não existem aqui (continuam no perfil ao salvar).
public sealed partial class EditorBarraTarefasViewModel(Func<IReadOnlyList<ProgramaBandeja>> programas) : ObservableObject
{
    [ObservableProperty] private PesquisaOpcional _pesquisa;
    [ObservableProperty] private Escolha _visaoTarefas;
    [ObservableProperty] private Escolha _mostrarAreaDeTrabalho;
    [ObservableProperty] private AgruparOpcional _agrupar;
    [ObservableProperty] private Escolha _ocultarAutomaticamente;
    [ObservableProperty] private Escolha _todosOsMonitores;
    [ObservableProperty] private Escolha _selos;
    [ObservableProperty] private Escolha _piscar;

    /// Sim = "Escolher" (a lista vale); NaoMexer = não mexer nos ícones.
    [ObservableProperty] private Escolha _escolherIcones;

    public ObservableCollection<ItemIcone> Icones { get; } = [];

    public void Carregar(BarraTarefasPerfil? b)
    {
        Pesquisa = b?.Pesquisa switch
        {
            PesquisaBarra.Oculta => PesquisaOpcional.Oculta,
            PesquisaBarra.Icone => PesquisaOpcional.Icone,
            PesquisaBarra.Caixa => PesquisaOpcional.Caixa,
            _ => PesquisaOpcional.NaoMexer,
        };
        VisaoTarefas = Para(b?.VisaoTarefas);
        MostrarAreaDeTrabalho = Para(b?.MostrarAreaDeTrabalho);
        Agrupar = b?.Agrupar switch
        {
            AgruparBotoes.Sempre => AgruparOpcional.Sempre,
            AgruparBotoes.QuandoCheia => AgruparOpcional.QuandoCheia,
            AgruparBotoes.Nunca => AgruparOpcional.Nunca,
            _ => AgruparOpcional.NaoMexer,
        };
        OcultarAutomaticamente = Para(b?.OcultarAutomaticamente);
        TodosOsMonitores = Para(b?.TodosOsMonitores);
        Selos = Para(b?.Selos);
        Piscar = Para(b?.Piscar);
        EscolherIcones = b?.IconesVisiveis is null ? Escolha.NaoMexer : Escolha.Sim;
        MontarIcones(b?.IconesVisiveis ?? []);
    }

    private void MontarIcones(IReadOnlyList<string> visiveis)
    {
        Icones.Clear();
        var daqui = programas();
        foreach (var p in daqui)
            Icones.Add(new ItemIcone(p.Executavel, p.Nome, encontrado: true) { Visivel = visiveis.Contains(p.Executavel, StringComparer.OrdinalIgnoreCase) });
        foreach (var exe in visiveis.Where(v => !daqui.Any(p => string.Equals(p.Executavel, v, StringComparison.OrdinalIgnoreCase))))
            Icones.Add(new ItemIcone(exe, Path.GetFileNameWithoutExtension(exe), encontrado: false) { Visivel = true });
    }

    public BarraTarefasPerfil? ParaPerfil()
    {
        var b = new BarraTarefasPerfil(
            Pesquisa switch
            {
                PesquisaOpcional.Oculta => PesquisaBarra.Oculta,
                PesquisaOpcional.Icone => PesquisaBarra.Icone,
                PesquisaOpcional.Caixa => PesquisaBarra.Caixa,
                _ => null,
            },
            De(VisaoTarefas),
            De(MostrarAreaDeTrabalho),
            Agrupar switch
            {
                AgruparOpcional.Sempre => AgruparBotoes.Sempre,
                AgruparOpcional.QuandoCheia => AgruparBotoes.QuandoCheia,
                AgruparOpcional.Nunca => AgruparBotoes.Nunca,
                _ => null,
            },
            De(OcultarAutomaticamente),
            De(TodosOsMonitores),
            De(Selos),
            De(Piscar),
            EscolherIcones == Escolha.Sim ? Icones.Where(i => i.Visivel).Select(i => i.Executavel).ToList() : null);
        return b == new BarraTarefasPerfil() ? null : b;
    }

    private static Escolha Para(bool? b) => b switch { true => Escolha.Sim, false => Escolha.Nao, null => Escolha.NaoMexer };
    private static bool? De(Escolha e) => e switch { Escolha.Sim => true, Escolha.Nao => false, _ => null };
}
