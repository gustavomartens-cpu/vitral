# Atualização automática — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** o Vitral se atualiza sozinho pelas releases do GitHub (com aviso no notch), e o repositório ganha os guias de instalação, atualização e publicação.

**Architecture:**

- A regra de quando procurar, baixar e avisar fica no Core (`ServicoAtualizacao`, com fonte e relógio injetados) e tem testes.
- O App liga essa regra ao Velopack (`UpdateManager` + `GithubSource`) com o token de leitura embutido no build.
- O notch ganha o aviso de atualização.
- O `build.ps1` ganha o `-Publicar`.
- Quatro documentos explicam o uso e a publicação.

**Tech Stack:** .NET 10, WPF, Velopack 1.2.161 (biblioteca e `vpk`), xUnit.

**Spec:** `docs/superpowers/specs/2026-10-26-atualizacao-design.md`

## Global Constraints

- Nenhum token no código, no git ou em arquivo do repositório. Eles vêm das variáveis `VITRAL_TOKEN_ATUALIZACAO` (leitura, embutido no build) e `VITRAL_TOKEN_PUBLICAR` (escrita, só no `-Publicar`) (spec §3).
- Repositório: `https://github.com/gustavomartens-cpu/vitral`. Pré-releases ficam de fora.
- Tempos: a primeira conferência 1 min depois de abrir, depois a cada 6 h; o App chama `TiqueAsync` a cada minuto.
- Uma falha ou a falta de internet nunca geram aviso no notch, só a linha de estado na janela.
- Português no código, nos comentários e nos documentos; `TreatWarningsAsErrors`; parar o `Vitral` antes de compilar o App.
- Teste: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet test --nologo -v q'`.
- Publicar uma release e instalar só com a autorização do Gustavo, em cada vez.

## Review Focus

1. **Build sem a variável de leitura:** o app compila e a atualização fica "desligada", sem lançar exceção. Testes `TokenEmbutido_ausente_e_null` e `Fonte_desligada_nunca_confere`.
2. **Sem internet, ou o GitHub fora do ar:** nenhum aviso no notch, a linha de estado mostra "Não foi possível procurar atualizações", e a conferência volta no ciclo seguinte. Teste `Falha_comum_vai_a_Falhou_e_tenta_de_novo`.
3. **Token vencido:** a linha mostra o acesso negado. Teste `Http_401_ou_403_vai_a_AcessoNegado`.
4. **Reiniciar com a doca ligada, ou com um pedido de permissão aberto:** a limpeza de saída roda antes da troca, e a barra do Windows não fica escondida. Conferência na Task 6.
5. **Um `vpk upload` que falha no meio:** a release fica como rascunho ou incompleta. O `build.ps1` para com a mensagem do `vpk`, e o `docs/publicar.md` diz como conferir e apagar a release incompleta. Conferência na Task 6.

---

### Task 1: Regra da atualização (Core)

**Files:**
- Create: `src/Vitral.Core/Atualizacao/ServicoAtualizacao.cs` (com `IFonteAtualizacao`, `EstadoAtualizacao` e `TextoAtualizacao`)
- Test: `tests/Vitral.Core.Tests/AtualizacaoTests.cs`

**Interfaces:**
- Produces (namespace `Vitral.Core.Atualizacao`):
  - `interface IFonteAtualizacao`, com `bool Ligada`, `Task<string?> ProcurarAsync()`, `Task BaixarAsync()` e `void Reiniciar()`.
  - `enum EstadoAtualizacao { Desligada, Atualizado, Baixando, Pronta, Falhou, AcessoNegado }`.
  - `sealed class ServicoAtualizacao(IFonteAtualizacao fonte, Func<DateTime> agora)`:
    - propriedades `Estado` e `Versao` (string?);
    - eventos `Mudou` e `Pronta` (`Action<string>`);
    - métodos `Task TiqueAsync()`, `Task ProcurarAgoraAsync()` e `void Reiniciar()`;
    - constantes `PrimeiraConferencia = 1 min` e `Intervalo = 6 h`.
  - `static class TextoAtualizacao`, com `string Para(EstadoAtualizacao e, string? versao)` e os textos do spec §5.4. No `Pronta`, o texto é "Versão {v} pronta — reiniciar".
  - Classifica como `AcessoNegado` uma `HttpRequestException` com `StatusCode` 401 ou 403, inclusive quando vier como `InnerException`.

- [ ] **Step 1: Testes que falham** (`AtualizacaoTests`), com uma `FonteFalsa` (versão a devolver, exceção a lançar, contadores) e um relógio falso:
  - `Nao_confere_antes_de_um_minuto`: `TiqueAsync` em 59 s não chama a fonte; em 61 s chama uma vez.
  - `Confere_de_novo_em_seis_horas`: depois da primeira, nenhuma chamada em 5 h 59 min; uma em 6 h 1 min.
  - `Versao_nova_baixa_e_avisa_uma_vez`: `ProcurarAsync` devolve "0.7.3" → os estados passam por `Baixando` e `Pronta`; `Pronta("0.7.3")` dispara uma vez; `BaixarAsync` é chamado uma vez; num tique seguinte, depois de 6 h, a fonte não é chamada.
  - `Mesma_versao_fica_atualizado`: `ProcurarAsync` devolve null → `Atualizado`.
  - `Falha_comum_vai_a_Falhou_e_tenta_de_novo`: a fonte lança `HttpRequestException` sem status → `Falhou`, sem `Pronta`; no ciclo seguinte, chama de novo.
  - `Http_401_ou_403_vai_a_AcessoNegado` (teoria com 401 e 403).
  - `Fonte_desligada_nunca_confere`: `Ligada = false` → `Desligada`, e nenhum tique chama a fonte.
  - `Procurar_agora_confere_fora_do_ciclo`.
  - `Textos_da_linha_de_estado`: um caso por estado.
- [ ] **Step 2: Rodar** → FAIL. **Step 3: Implementar.** **Step 4: Rodar** → PASS.
- [ ] **Step 5: Commit** `feat(core): regra da atualização automática`.

---

### Task 2: Token embutido no build

**Files:**
- Create: `src/Vitral.Core/Atualizacao/TokenEmbutido.cs`
- Modify: `src/Vitral.App/Vitral.App.csproj`, `build.ps1`
- Test: `tests/Vitral.Core.Tests/AtualizacaoTests.cs`

**Interfaces:**
- Produces:
  - `TokenEmbutido.Ler(Assembly) → string?`: lê `AssemblyMetadataAttribute` com a chave `TokenAtualizacao`; vazio ou ausente devolve null.
  - **No csproj:** `<ItemGroup Condition="'$(TokenAtualizacao)' != ''"><AssemblyMetadata Include="TokenAtualizacao" Value="$(TokenAtualizacao)" /></ItemGroup>`.
  - **No `build.ps1`:** se `$env:VITRAL_TOKEN_ATUALIZACAO` existir, o `dotnet publish` recebe `-p:TokenAtualizacao=$env:VITRAL_TOKEN_ATUALIZACAO`. Se não existir, o script avisa em amarelo: "sem VITRAL_TOKEN_ATUALIZACAO: esta versão não se atualiza sozinha". O token nunca aparece na saída.
- [ ] **Step 1: Testes que falham:**
  - `TokenEmbutido_ausente_e_null`: o assembly de teste não tem o metadado.
  - `TokenEmbutido_presente`: um `AssemblyMetadata("TokenAtualizacao","abc")` no assembly de teste, num arquivo `AssemblyInfoTeste.cs` só do projeto de teste. Com outra chave, o teste confere o ausente pela chave.

    Para os dois casos, `Ler` aceita a chave por parâmetro opcional: `Ler(Assembly, string chave = "TokenAtualizacao")`.
- [ ] **Step 2: Rodar** → FAIL. **Step 3: Implementar** `TokenEmbutido`, o csproj e o `build.ps1`. **Step 4: Rodar** → PASS.
- [ ] **Step 5: Conferir:**
  - um `dotnet publish` local com `-p:TokenAtualizacao=teste123` grava o metadado; conferir o `Vitral.dll` publicado lendo o metadado por PowerShell (reflexão);
  - sem a propriedade, ele não aparece.
- [ ] **Step 6: Commit** `feat: token de leitura embutido no build`.

---

### Task 3: Aviso de atualização no notch (Ui)

**Files:**
- Modify: `src/Vitral.Core/Claude/AvisoClaude.cs` (`TipoAviso.Atualizacao`), `src/Vitral.Ui/Barra/EstadoNotch.cs` (`AcaoNotch.Reiniciar`)
- Test: `tests/Vitral.Ui.Tests/NotchTests.cs`

**Interfaces:**
- Produces:
  - `TipoAviso.Atualizacao`;
  - `AcaoNotch.Reiniciar`: clicar no meio durante um aviso de `Atualizacao` devolve `Reiniciar` (os outros avisos continuam com `AbrirClaude`);
  - no lembrete de atualização, a lateral esquerda também devolve `Reiniciar`.
- [ ] **Step 1: Testes que falham:**
  - `Clique_no_aviso_de_atualizacao_reinicia`;
  - `Lembrete_de_atualizacao_reinicia_pela_lateral`;
  - `Aviso_do_Claude_continua_abrindo_o_Claude`.
- [ ] **Step 2: Rodar** → FAIL. **Step 3: Implementar.** **Step 4: Rodar** → PASS.
- [ ] **Step 5: Commit** `feat(ui): aviso de atualização no notch`.

---

### Task 4: Atualizador no App

**Files:**
- Create: `src/Vitral.App/Atualizacao/FonteVelopack.cs`
- Modify:
  - `src/Vitral.App/App.xaml.cs`: o serviço, o tique de 1 min, `Reiniciar` com a limpeza;
  - `src/Vitral.App/Barra/JanelaNotch.cs`: logo do Vitral, os textos e a lateral do aviso de atualização;
  - `src/Vitral.App/Barra/ControleBarra.cs`: `Avisar` repassa;
  - `src/Vitral.App/JanelaPrincipal.xaml(.cs)`: linha de estado e "Procurar agora".

**Interfaces:**
- Consumes: Tasks 1, 2 e 3.
- Produces:
  - **`FonteVelopack(string? token)`:**
    - `Ligada` = token presente e `UpdateManager.IsInstalled`;
    - `ProcurarAsync`, `BaixarAsync` e `Reiniciar(Action limpezaDeSaida)`, que chama a limpeza, depois `WaitExitThenApplyUpdates(info.TargetFullRelease, silent: true, restart: true)` e `Shutdown`;
    - o `UpdateManager` é criado só quando `Ligada`.
  - **`App`:**
    - `Sair()` é dividido em `LimparParaSair()` (tudo menos o `Shutdown`) e `Shutdown()`;
    - cria `ServicoAtualizacao(new FonteVelopack(TokenEmbutido.Ler(typeof(App).Assembly)), () => DateTime.Now)`;
    - um `DispatcherTimer` de 1 min chama `TiqueAsync`;
    - `Pronta(v)` vira `_barra?.Avisar(new AvisoClaude(TipoAviso.Atualizacao, "Vitral", $"{v} pronto"))`;
    - o `Reiniciar` do notch chama o do serviço.
  - **`JanelaNotch`**, para `TipoAviso.Atualizacao`:
    - símbolo `Logo` (do Vitral) no lugar do símbolo do Claude;
    - título "Vitral {versão} pronto" e linha "clique para reiniciar e atualizar";
    - lateral esquerda "Atualização pronta";
    - a ação `Reiniciar` chama um `Action` vindo do `ControleBarra`/`App`.
  - **`JanelaPrincipal`:** um `TextBlock` com `TextoAtualizacao.Para(...)` embaixo da versão e um `Hyperlink` "Procurar agora". No estado `Pronta`, o "reiniciar" também é clicável. Atualizam com o `Mudou`.
- [ ] **Step 1: Implementar.**
- [ ] **Step 2: Build** sem avisos e testes → PASS.
- [ ] **Step 3: Conferir na build de desenvolvimento:**
  - a janela mostra "Atualização desligada (versão de desenvolvimento)";
  - "Procurar agora" não faz nada e não dá erro;
  - um aviso de atualização simulado por um atalho de depuração (`#if DEBUG`, com a tecla F9 na janela principal chamando `Avisar(Atualizacao, "0.0.0-teste")`) aparece com o logo do Vitral; o clique fecha e reabre o Vitral pela limpeza normal, mas sem trocar a versão, porque sem `UpdateManager` só reinicia o processo.
- [ ] **Step 4: Verificação com o Gustavo:** o aviso de atualização simulado e a linha de estado.
- [ ] **Step 5: Commit** `feat: atualizador automático no Vitral`.

---

### Task 5: Publicação e documentação

**Files:**
- Modify: `build.ps1` (`-Publicar`)
- Create: `README.md`, `docs/atualizacao.md`, `docs/publicar.md`
- Rename + modify: `docs/instalar-no-notebook.md` → `docs/instalar.md`

**Interfaces:**
- Produces:
  - **`build.ps1 -Publicar`:**
    1. Exige `VITRAL_TOKEN_ATUALIZACAO` e `VITRAL_TOKEN_PUBLICAR`; se faltar uma, para com a mensagem "Defina VITRAL_TOKEN_... (veja docs/publicar.md)".
    2. Roda `vpk download github` antes do `pack` e `vpk upload github` (com `--publish`, `--tag v$Versao` e `--releaseName "Vitral $Versao"`) depois. As notas vêm de `docs/notas/$Versao.md`, se existir.
    3. Se o `vpk` sair com erro, o script para com a saída dele.
  - **Documentos:** conforme o spec §6, com os passos do GitHub escritos tela a tela.
- [ ] **Step 1: Implementar** o `-Publicar` e os quatro documentos. O `README.md` aponta para os três guias e para `docs/ideias-pendentes.md` como roteiro.
- [ ] **Step 2: Conferir o `-Publicar`** sem as variáveis: ele para com a mensagem certa, sem publicar nada.
- [ ] **Step 3: Revisão dos textos com o Gustavo:** ele lê o `docs/publicar.md` e confere se dá para seguir sem ajuda.
- [ ] **Step 4: Commit** `docs: instalar, atualizar e publicar o Vitral`.

---

### Task 6: Teste prático (com o Gustavo)

- [ ] **Step 1:** o Gustavo cria os dois tokens seguindo o `docs/publicar.md` e define as variáveis. Eu não vejo nem manipulo os valores; confiro só que as variáveis existem, com `[bool]$env:...`.
- [ ] **Step 2:** com a autorização dele, publicar a `0.7.2` (`pwsh build.ps1 -Versao 0.7.2 -Publicar`); conferir a release (arquivos, a tag e que não é rascunho) e instalar a 0.7.2 neste PC.
- [ ] **Step 3:** o Gustavo instala a 0.7.2 no notebook, pelo Setup da release.
- [ ] **Step 4:** com a autorização dele, publicar a `0.7.3`, sem mudanças, e conferir nos dois computadores:
  - o aviso aparece no notch;
  - o clique reinicia e atualiza: a versão na janela passa a 0.7.3, e a doca e o perfil voltam;
  - num dos dois, sem clicar: fechar e abrir o Vitral faz a 0.7.3 entrar.
- [ ] **Step 5:** registrar no ledger e ajustar o que aparecer, com teste primeiro.

---

### Task 7: Fechamento

- [ ] **Step 1:** suítes completas → PASS; build sem avisos.
- [ ] **Step 2: Revisão final** por um agente Opus, com o Review Focus, mais:
  - o token nunca aparecer em log, em mensagem ou na saída do build;
  - a limpeza de saída antes da troca;
  - concorrência entre o tique e o "Procurar agora".

  Corrigir os itens Crítico e Importante com teste primeiro.
- [ ] **Step 3:** com o ok do Gustavo, juntar na main e enviar ao GitHub. A partir daqui, as versões saem pelo `-Publicar`.
