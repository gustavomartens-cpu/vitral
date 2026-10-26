# Permissões do Claude pelo notch — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** os pedidos de permissão do Claude Code aparecem no notch com Permitir, Sempre nesta sessão e Negar, e a resposta volta ao Claude pelo canal.

**Architecture:**

- **Contas e regras puras:**
  - `PedidoPermissao`, `RespostaPermissao`, as linhas do protocolo, `GanchosClaude` e `Canal.Perguntar`, no Core;
  - os estados do pedido, no `EstadoNotch` (Ui).
- **App:**
  - o mensageiro espera a resposta no `PermissionRequest`;
  - o `CanalAvisos` atende várias conexões e responde na mesma conexão;
  - a `JanelaNotch` ganha a ficha do pedido.

**Tech Stack:** .NET 10, WPF, System.Text.Json, System.IO.Pipes, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-26-permissoes-notch-design.md`

## Global Constraints

- O mensageiro nunca falha para o Claude: o código de saída é sempre 0, e sem resposta não escreve nada. Os prazos são 65 s no `PermissionRequest` e 1,5 s nos demais eventos.
- Medidas da ficha: altura 112, largura até 420, raio 24; botões de 28 px; barra de 2 px; tempo de 60 s contado desde que o pedido chegou (spec §4).
- "Sempre nesta sessão" usa sempre `destination: session` (spec §3.3).
- O gancho `PermissionRequest` leva `"timeout": 75` (spec §3.1).
- Canal: até 4 instâncias, a mesma ACL e linhas de no máximo 4 KB (spec §3.5).
- Português no código e nos comentários; `TreatWarningsAsErrors`; parar o `Vitral` antes de compilar o App.
- Teste: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet test --nologo -v q'`.

## Review Focus

1. **Pedido respondido no app do Claude:** o notch não pode ficar preso nele. Ele sai com o próximo aviso da sessão ou no fim dos 60 s, e o canal completa com null, sem escrever nada. Testes da Task 3.
2. **Vários pedidos ao mesmo tempo, de sessões diferentes:** cada resposta volta para o pedido certo (o id confere), e nenhum mensageiro fica esperando além de 65 s. Testes das Tasks 1, 2 e 4.
3. **Vitral fechado, travado ou fechando no meio de um pedido:** o mensageiro sai sem decisão e o Claude segue o fluxo normal. Testes `Perguntar_sem_resposta_da_null_no_prazo` e `Sem_servidor_null_rapido` (Task 2), e conferência na Task 4.
4. **"Sempre nesta sessão" com sugestão de outro destino, ou sem sugestão:** a regra sai sempre com `session`, nunca gravada. Testes da Task 1.
5. **Caixa marcada da fase anterior, só com dois ganchos:** aparece desmarcada, e marcar acrescenta o terceiro sem duplicar. Testes da Task 2.

---

### Task 1: Pedido e resposta (Core)

**Files:**
- Create: `src/Vitral.Core/Claude/PedidoPermissao.cs` (com `RespostaPermissao` e `enum Escolha`)
- Test: `tests/Vitral.Core.Tests/PermissaoTests.cs`

**Interfaces:**
- Produces (namespace `Vitral.Core.Claude`):
  - `enum Escolha { Permitir, SempreNaSessao, Negar }`
  - `sealed record PedidoPermissao(string Id, string Sessao, string Ferramenta, string Titulo, string Resumo)` com:
    - `static PedidoPermissao? DeGancho(string json)`: um GUID novo como `Id`, a tabela do spec §3.2 e o resumo numa linha de no máximo 200 caracteres (usa `AvisoClaude.Encurtar` com 200);
    - `string ParaLinha()`: `{"tipo":"Permissao","id","sessao","ferramenta","titulo","resumo"}`;
    - `static PedidoPermissao? DeLinha(string? linha)`: só quando `tipo == "Permissao"`, e com no máximo 4096 caracteres;
    - `static bool EhPedido(string? linha)`: o JSON tem `"tipo":"Permissao"`.
  - `static class RespostaPermissao` com:
    - `static string Linha(string id, Escolha e)`: `{"id":…,"escolha":"Permitir|SempreNaSessao|Negar"}`;
    - `static Escolha? DaLinha(string? linha, string idEsperado)`: null se o id for diferente ou a linha for inválida;
    - `static string ParaClaude(Escolha e, string jsonDoGancho)`: o spec §3.3, com `JsonNode` e saída compacta.

- [ ] **Step 1: Testes que falham** (`PermissaoTests`):
  - `Bash_vira_rodar_um_comando`: `{"hook_event_name":"PermissionRequest","cwd":"G:\\dev\\vitral","tool_name":"Bash","tool_input":{"command":"dotnet test\n--nologo"}}` → título "Claude quer rodar um comando", resumo "dotnet test --nologo", sessão "vitral", ferramenta "Bash", `Id` não vazio.
  - `Edit_e_Write_mostram_o_nome_do_arquivo`: `Edit`/`MultiEdit` com `file_path` "C:\\a\\b\\Foo.cs" → "Claude quer editar um arquivo" / "Foo.cs"; `Write` → "Claude quer criar um arquivo".
  - `WebFetch_mostra_o_endereco`, `Mcp_mostra_o_ultimo_trecho` (`mcp__sic__lead_get` → "lead_get"), `Outra_ferramenta_mostra_o_nome`.
  - `Campo_ausente_usa_o_nome_da_ferramenta`.
  - `Resumo_cortado_em_200`.
  - `Evento_errado_ou_json_ruim_e_null`: `Stop`, `""` e `"{x"` devolvem null.
  - `Linha_do_pedido_ida_e_volta`; `EhPedido_distingue_de_aviso` (a linha de um `AvisoClaude` dá false).
  - `Resposta_com_id_certo_e_errado`: `DaLinha(Linha("a", Negar), "a")` dá `Negar`; com "b", dá null; `"{"` dá null.
  - `Permitir_vira_allow`: o JSON tem `hookSpecificOutput.hookEventName = "PermissionRequest"` e `decision.behavior = "allow"`, sem `updatedPermissions`.
  - `Sempre_usa_a_sugestao_com_destino_sessao`: a sugestão `addRules` traz `destination: "localSettings"` → sai `"session"`, com as mesmas `rules` e `behavior: "allow"`.
  - `Sempre_sem_sugestao_no_Bash_usa_o_comando`: `rules = [{toolName:"Bash", ruleContent:"npm test"}]`.
  - `Sempre_sem_sugestao_em_outra_ferramenta_usa_a_ferramenta`: `rules = [{toolName:"WebFetch"}]`.
  - `Negar_vira_deny_com_recado`: `behavior: "deny"`, `message: "Negado no notch do Vitral."`, `interrupt: false`.
- [ ] **Step 2: Rodar** → FAIL. **Step 3: Implementar.** **Step 4: Rodar** → PASS.
- [ ] **Step 5: Commit** `feat(core): pedido e resposta de permissão do Claude`.

---

### Task 2: Ganchos com o terceiro evento e o canal que pergunta (Core)

**Files:**
- Modify: `src/Vitral.Core/Claude/GanchosClaude.cs`, `src/Vitral.Core/Claude/Canal.cs`
- Test: `tests/Vitral.Core.Tests/GanchosClaudeTests.cs`, `tests/Vitral.Core.Tests/CanalTests.cs`

**Interfaces:**
- Produces:
  - em `GanchosClaude`, os eventos passam a ser `Stop`, `Notification` e `PermissionRequest`; o item do `PermissionRequest` ganha `"timeout": 75`, e `EstaLigado` exige os três;
  - `Canal.Perguntar(string nomeCanal, string linha, int esperaRespostaMs, int esperaConexaoMs = 300) → string?`: conecta `InOut` (`PipeOptions.Asynchronous | CurrentUserOnly`), escreve a linha com `\n` e lê até o `\n` (no máximo 4096 caracteres) com prazo total de `esperaRespostaMs`. Em qualquer falha, devolve null.

- [ ] **Step 1: Testes que falham:**
  - `Ligar_escreve_os_tres_eventos_com_timeout_na_permissao`: um item nosso em cada evento, e `hooks.PermissionRequest[0].hooks[0].timeout == 75`.
  - `So_os_dois_antigos_conta_como_desligado_e_ligar_completa`: um texto com só `Stop`/`Notification` nossos → `EstaLigado` false; `Ligar` → true, com um item nosso por evento.
  - `Desligar_tira_os_tres`.
  - `Perguntar_recebe_a_resposta`: um servidor de teste (`InOut`) lê a linha e responde "ok" → "ok".
  - `Perguntar_sem_resposta_da_null_no_prazo`: o servidor aceita e não responde → null entre 300 ms e 1 s, com `esperaRespostaMs` 400.
  - `Sem_servidor_null_rapido`: menos de 1 s.
- [ ] **Step 2: Rodar** → FAIL. **Step 3: Implementar.** **Step 4: Rodar** → PASS, com os testes antigos de ganchos ajustados ao terceiro evento.
- [ ] **Step 5: Commit** `feat(core): gancho de permissão e canal que pergunta`.

---

### Task 3: Estados do pedido no notch (Ui)

**Files:**
- Modify: `src/Vitral.Ui/Barra/EstadoNotch.cs`, `src/Vitral.Ui/Barra/FormaNotch.cs` (`NotchMedidas`)
- Test: `tests/Vitral.Ui.Tests/NotchTests.cs`

**Interfaces:**
- Consumes: Task 1 (`PedidoPermissao`, `Escolha`).
- Produces:
  - `ModoNotch.Permissao`;
  - `NotchMedidas`: `AlturaPermissao = 112`, `LarguraMaxPermissao = 420`, `RaioPermissao = 24`, e `JanelaAltura = NotchSobra + AlturaPermissao`;
  - em `EstadoNotch`:
    - `const double TempoPedido = 60`;
    - propriedades `PedidoAtual` e `NaFila` (int);
    - `bool Confirmando` e `Escolha? UltimaEscolha`, para a confirmação de 1 s;
    - `void Pediu(PedidoPermissao p)`;
    - `PedidoPermissao? Respondeu(Escolha e)`;
    - `event Action<PedidoPermissao, Escolha?>? Encerrado`;
    - no `Tique`: os 60 s por pedido (o atual e os da fila), o fim da confirmação depois de 1 s e a passagem ao próximo;
    - `Chegou(aviso)` descarta os pedidos da mesma sessão (`Encerrado` com null);
    - um aviso de outra sessão durante um pedido vira `Pendente`, sem trocar o modo;
    - quando o último pedido sai sem resposta: `Lembrete` + `Pendente` + `PedidoPerdido = true`, para a lateral esquerda mostrar "Pedido sem resposta".

- [ ] **Step 1: Testes que falham:**
  - `Pedido_abre_a_ficha`: `Pediu` → modo `Permissao`, com `PedidoAtual` igual ao pedido.
  - `Responder_encerra_com_a_escolha_e_confirma`: `Respondeu(Permitir)` → `Encerrado(p, Permitir)` uma vez; `Confirmando` com `UltimaEscolha`; depois de 1,1 s, `Normal`.
  - `Sessenta_segundos_sem_resposta_encerra_com_null`: aos 59,9 s ainda é `Permissao`; aos 60,1 s, `Encerrado(p, null)`, `Lembrete` e `PedidoPerdido`.
  - `Fila_com_mais_um`: dois `Pediu` → `NaFila == 1`; responder o primeiro e passar a confirmação faz o segundo virar o atual.
  - `Tempo_do_segundo_conta_desde_que_ele_chegou`: o segundo chega em t=50; responder o primeiro em t=55; o segundo encerra com null em t=110,1, e não em t=115.
  - `Aviso_da_mesma_sessao_descarta_o_pedido`: `Encerrado(p, null)`, e segue o aviso.
  - `Aviso_de_outra_sessao_nao_toma_o_notch`: o modo continua `Permissao` e `Pendente` fica true.
  - `Mouse_em_cima_nao_segura_o_pedido`.
- [ ] **Step 2: Rodar** → FAIL. **Step 3: Implementar.** **Step 4: Rodar** → PASS, com os testes antigos do notch passando.
- [ ] **Step 5: Commit** `feat(ui): estados do pedido de permissão no notch`.

---

### Task 4: Mensageiro e canal de mão dupla (App)

**Files:**
- Modify: `src/Vitral.App/Claude/Mensageiro.cs`, `src/Vitral.App/Program.cs`, `src/Vitral.App/Claude/CanalAvisos.cs`, `src/Vitral.App/App.xaml.cs`, `src/Vitral.App/Barra/ControleBarra.cs`

**Interfaces:**
- Consumes: Tasks 1 e 2.
- Produces:
  - **`Program.Main`:** no `--aviso`, lê a entrada **antes** de escolher o prazo; o `Mensageiro.Executar(entrada)` recebe o texto já lido. O prazo é `Mensageiro.EhPermissao(entrada) ? 65_000 : 1_500`.
  - **`Mensageiro`, no `PermissionRequest`:**
    - `Canal.Perguntar(nome, pedido.ParaLinha(), 60_000)` e `RespostaPermissao.DaLinha(resposta, pedido.Id)`;
    - com a escolha, `Console.Out.Write(RespostaPermissao.ParaClaude(e, entrada))` e `Flush`;
    - nos outros eventos, o caminho de hoje.
  - **`CanalAvisos`:**
    - até 4 servidores em laço (`maxNumberOfServerInstances: 4`, `InOut`, buffers de 4096);
    - cada conexão aceita vai para uma tarefa própria, e o laço já cria a próxima instância;
    - aviso: segue como hoje;
    - pedido: `chegouPedido(pedido)` devolve `Task<Escolha?>`, que vem do notch pelo `Dispatcher`; com a escolha, escreve `RespostaPermissao.Linha` e fecha; com null, fecha sem escrever;
    - o construtor ganha `Func<PedidoPermissao, Task<Escolha?>> chegouPedido`; sem barra do topo, devolve null na hora.
  - **`ControleBarra.Pedir(PedidoPermissao) → Task<Escolha?>`:** repassa ao notch. Sem notch visível, devolve null. A Task 5 faz a ficha; aqui o notch só registra no log e devolve null depois de 2 s, para conferir o canal.

- [ ] **Step 1: Implementar.**
- [ ] **Step 2: Build** sem avisos e testes → PASS.
- [ ] **Step 3: Conferir**, enviando pelo PowerShell um `PermissionRequest` de teste ao `Vitral.exe --aviso` de desenvolvimento:
  - com o Vitral aberto: o log mostra o pedido e o comando sai em ~2 s com código 0, sem nada na saída padrão;
  - com o Vitral fechado: sai em menos de 0,5 s, com código 0;
  - dois pedidos ao mesmo tempo: os dois aparecem no log.
- [ ] **Step 4: Commit** `feat: canal de mão dupla para os pedidos de permissão`.

---

### Task 5: Ficha do pedido no notch (App)

**Files:**
- Modify: `src/Vitral.App/Barra/JanelaNotch.cs`, `src/Vitral.App/Barra/ControleBarra.cs`

**Interfaces:**
- Consumes: Task 3 (os estados), Task 4 (`Pedir`).
- Produces:
  - **Painel da ficha na `JanelaNotch`:**
    - a linha do pedido: ícone, título e "sessão · resumo" em fonte de código, com o resumo inteiro como dica;
    - os três botões em pílula, com `Efeitos.Afundar`, raio pela meia altura e as cores do spec §4.1;
    - a barra de 2 px, com `ScaleTransform` X de 1 a 0 em 60 s, menos o tempo que o pedido já esperou; parada com a animação desligada;
    - o "+N" da fila;
    - a confirmação de 1 s.
  - **`JanelaNotch.Pedir(PedidoPermissao) → Task<Escolha?>`:**
    - guarda um `TaskCompletionSource` por `Id` e chama `EstadoNotch.Pediu`;
    - o `Encerrado` completa a tarefa;
    - clicar num botão chama `EstadoNotch.Respondeu`.
  - **Lateral esquerda:** com `PedidoPerdido`, mostra "Pedido sem resposta", e o clique abre o Claude.
  - **`ControleBarra.Pedir`** passa a usar a ficha.

- [ ] **Step 1: Implementar.**
- [ ] **Step 2: Build** sem avisos e testes → PASS.
- [ ] **Step 3: Conferir com pedidos simulados** pelo PowerShell; a saída padrão mostra o JSON devolvido:
  - Permitir, Sempre e Negar;
  - deixar esgotar os 60 s;
  - dois pedidos seguidos, com o "+1";
  - um aviso da mesma sessão durante o pedido.
- [ ] **Step 4: Verificação com o Gustavo:** a ficha, os botões, a barra, o "+1", a confirmação e o lembrete "Pedido sem resposta".
- [ ] **Step 5: Commit** `feat: ficha de permissão no notch`.

---

### Task 6: Teste real com o Claude

- [ ] **Step 1:** o Gustavo desmarca e marca a caixa "Avisos do Claude no notch" (agora com os três ganchos), e eu confiro o `settings.json`: três eventos e `timeout` 75.
- [ ] **Step 2:** um pedido real deste Claude, um comando que precise de permissão, respondido pelo notch nos três jeitos, conferindo o efeito de cada um:
  - **Permitir:** o comando roda.
  - **Sempre nesta sessão:** o mesmo comando, de novo, não pede mais.
  - **Negar:** o Claude recebe o recado.
- [ ] **Step 3:** um pedido respondido no app; o notch limpa com o aviso seguinte da sessão.
- [ ] **Step 4:** conferir no Claude Code de terminal se o pedido dele aparece enquanto o gancho espera, e anotar no ledger. Se não aparecer, levar a escolha ao Gustavo (spec §6).
- [ ] **Step 5:** commit de eventuais ajustes, com teste primeiro.

---

### Task 7: Fechamento e instalador 0.8.0

- [ ] **Step 1:** suítes completas → PASS; build sem avisos.
- [ ] **Step 2: Revisão final** por um agente Opus, com o Review Focus acima, mais:
  - as tarefas do canal com conexões pendentes no `Sair`;
  - `TaskCompletionSource` nunca completados;
  - a ordem das respostas com 4 conexões.

  Corrigir os itens Crítico e Importante com teste primeiro.
- [ ] **Step 3:** com autorização: versão `0.8.0`, build, instalação silenciosa e abertura; commit; com o ok do Gustavo, juntar na main, enviar ao GitHub, publicar a release `v0.8.0` e marcar a caixa de novo no Vitral instalado.
