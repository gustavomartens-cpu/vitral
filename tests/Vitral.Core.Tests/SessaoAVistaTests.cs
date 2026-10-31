using Vitral.Core.Claude;

namespace Vitral.Core.Tests;

public sealed class SessaoAVistaTests
{
    private const string Id = "local_c04c08d0-0d82-4510-9e74-3e5c1eb8c2d3";
    private const string Casca = "file:///C:/Program%20Files/WindowsApps/Claude_4.4212.0.0_x64__pzs8sxrjxfjjc/app/resources/app.asar/.vite/renderer/main_window/index.html";

    [Fact]
    public void Conversa_aberta_numa_janela_da_frente_esta_a_vista() =>
        Assert.True(ConversaClaude.AVista(Id, [new JanelaClaude(true, [Casca, $"https://claude.ai/epitaxy/{Id}"])]));

    [Fact]
    public void Outra_conversa_aberta_nao_esta_a_vista() =>
        Assert.False(ConversaClaude.AVista(Id, [new JanelaClaude(true, ["https://claude.ai/epitaxy/local_outra-sessao"])]));

    [Fact]
    public void Janela_coberta_ou_minimizada_nao_conta() =>
        Assert.False(ConversaClaude.AVista(Id, [new JanelaClaude(false, [$"https://claude.ai/epitaxy/{Id}"])]));

    [Fact]
    public void Qualquer_janela_da_frente_serve_entre_varias() =>
        Assert.True(ConversaClaude.AVista(Id, [new JanelaClaude(true, ["https://claude.ai/epitaxy/local_outra"]),
            new JanelaClaude(true, [$"https://claude.ai/epitaxy/{Id}?aba=x"])]));

    [Fact]
    public void Id_que_so_comeca_igual_nao_confunde() =>
        Assert.False(ConversaClaude.AVista("local_c04c", [new JanelaClaude(true, [$"https://claude.ai/epitaxy/{Id}"])]));

    [Fact]
    public void Sem_id_nao_sabe_e_conta_como_fora() =>
        Assert.False(ConversaClaude.AVista(null, [new JanelaClaude(true, [$"https://claude.ai/epitaxy/{Id}"])]));
}
