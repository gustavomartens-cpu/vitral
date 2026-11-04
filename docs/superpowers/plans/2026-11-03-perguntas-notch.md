# Perguntas do Claude no notch: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** a pergunta de escolha do Claude (`AskUserQuestion`) aparece no notch com as opções, e a escolha volta ao Claude como a resposta.

**Architecture:** a pergunta viaja pelo mesmo caminho do pedido de permissão (gancho `PermissionRequest` → `Vitral.exe --aviso` → canal → notch → canal → saída do gancho). O pedido ganha as perguntas, a resposta ganha as escolhas, o notch ganha a ficha de pergunta dentro do modo Permissao (mesma fila, mesmo tempo por pedido, mesma desistência).

**Tech Stack:** .NET 10, WPF, System.Text.Json, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-03-perguntas-notch-design.md`

## Global Constraints

- Testes: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet build --nologo -v q; dotnet test --nologo -v q --no-build'` (o `dotnet test` sozinho não recompila o Vitral.App); feche o Vitral de desenvolvimento antes.
- Português no código e nos textos; commits terminam com `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Textos exatos: cabeçalho "Claude pergunta"; botões "Enviar" e "Responder na conversa"; contador "1/2".
- Formato da resposta ao Claude (provado): `{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"allow","updatedInput":{"questions":<as do gancho, sem mudança>,"answers":{"<question>":"<label>"}}}}}`; várias escolhas = rótulos juntos com ", ".
- Tempos: ficha de pergunta 300 s (`TempoPergunta`), permissão 60 s; gancho `PermissionRequest` 330 s no settings.json; espera do mensageiro: pergunta 302 s, permissão continua 62 s; prazo total do `--aviso`: pergunta 310 s, permissão 65 s.
- Nunca escrever nada na saída do gancho para "Responder na conversa", tempo esgotado ou desistência.

## Review Focus

1. Pergunta com acento ou aspas no texto → a chave de `answers` é o texto exato da pergunta (Task 1, `Respostas_usam_o_texto_exato_da_pergunta`).
2. Pedido de permissão comum depois da mudança → a ficha e a saída são as de antes, sem `updatedInput` (Task 1, `Permissao_comum_nao_muda`).
3. Pergunta respondida no app enquanto está na ficha → sai sem resposta e sem contar como "pedido sem resposta" (Task 3, `Pergunta_respondida_no_app_sai`).
4. Duas perguntas: clique na primeira não envia nada; só a última envia as duas (Task 3, `Duas_perguntas_acumulam`).
5. Ganchos ligados com o tempo antigo (75 s) → religados com 330 s sem duplicar entradas (Task 2, `Gancho_com_tempo_antigo_e_religado`).

---

### Task 1: Perguntas no pedido e respostas na saída (Core)

**Files:**
- Modify: `src/Vitral.Core/Claude/PedidoPermissao.cs`
- Create: `src/Vitral.Core/Claude/Perguntas.cs`
- Test: `tests/Vitral.Core.Tests/PerguntasTests.cs`, ajustar `tests/Vitral.Core.Tests/PermissaoTests.cs`

**Interfaces (Produces):**
- `public sealed record Opcao(string Rotulo, string? Descricao)`
- `public sealed record Pergunta(string Texto, string? Cabecalho, IReadOnlyList<Opcao> Opcoes, bool Multipla)`
- `PedidoPermissao.Perguntas : IReadOnlyList<Pergunta>?` (init; nulo fora de `AskUserQuestion`); viaja em `ParaLinha`/`DeLinha`; linha grande demais vai sem as perguntas.
- `RegrasPerguntas.Cabe(IReadOnlyList<Pergunta>?) : bool` — 1 a 4 perguntas, cada uma com 1 a 4 opções.
- `enum EscolhaPermissao { Permitir, SempreNaSessao, Negar, Conversa }`
- `public sealed record RespostaPedido(EscolhaPermissao Escolha, IReadOnlyDictionary<string, string>? Respostas = null)` com conversão implícita de `EscolhaPermissao`.
- `RespostaPermissao.Linha(string id, RespostaPedido r)`, `DaLinha(string?, string) : RespostaPedido?`, `ParaClaude(RespostaPedido r, string jsonDoGancho) : string?` (nulo = não escrever nada: `Conversa`).
- `RegrasPerguntas.Juntar(Pergunta p, IEnumerable<string> rotulos) : string` — ", " entre rótulos, na ordem das opções.

- [ ] **Step 1: Testes** em `PerguntasTests`:
  - `Le_as_perguntas_do_gancho` — `DeGancho` de um `AskUserQuestion` com duas perguntas (uma `multiSelect`) devolve `Perguntas` com textos, rótulos, descrições e `Multipla` certos; `Titulo == "Claude pergunta"`.
  - `Perguntas_viajam_na_linha` — `DeLinha(p.ParaLinha())` traz as mesmas perguntas.
  - `Nao_cabe` — sem opções, 5 opções, 5 perguntas → `Cabe` falso; 1 pergunta com 2 opções → verdadeiro; nulo → falso.
  - `Respostas_usam_o_texto_exato_da_pergunta` — pergunta "Qual é a \"melhor\" opção?" → `answers` tem essa chave exata; `updatedInput.questions` igual ao do gancho.
  - `Varias_escolhas_juntam_com_virgula` — `Juntar` de "Verde","Azul" numa pergunta com Azul, Verde, Roxo → "Azul, Verde".
  - `Conversa_nao_escreve_nada` — `ParaClaude(EscolhaPermissao.Conversa, gancho)` é nulo.
  - `Resposta_viaja_na_linha` — `DaLinha(Linha("a", new RespostaPedido(Permitir, {"Q":"A"})), "a")` traz as respostas; id errado → nulo.
  - `Permissao_comum_nao_muda` — `ParaClaude(EscolhaPermissao.Permitir, gancho de Bash)` sem `updatedInput`; `Perguntas` nulo para Bash.
- [ ] **Step 2:** Rodar; falham por compilação (tipos novos).
- [ ] **Step 3:** Implementar. `ParaClaude` com `Respostas` não nulo → `allow` + `updatedInput` = `tool_input` do gancho clonado com `answers`. Ajustar as asserções de `PermissaoTests` que comparam `DaLinha` com o enum para `.Escolha`; o `Mensageiro` passa a pular a escrita quando `ParaClaude` devolve nulo.
- [ ] **Step 4:** Rodar; verdes, suíte inteira verde.
- [ ] **Step 5:** Commit `feat: perguntas no pedido e respostas na saída`.

### Task 2: Tempos (Core e mensageiro)

**Files:** `src/Vitral.Core/Claude/GanchosClaude.cs`, `src/Vitral.Core/Claude/Canal.cs`, `src/Vitral.App/Claude/Mensageiro.cs`; testes nos arquivos de teste existentes dos ganchos e do canal.

**Interfaces:** `Canal.EsperaPerguntaMs = 302_000`; `Mensageiro.PrazoMs` devolve 310 000 para `AskUserQuestion`; `GanchosClaude` grava `timeout` 330 no `PermissionRequest`; `EstaLigado` falso com o tempo antigo.

- [ ] **Step 1: Testes:** `Gancho_de_permissao_com_330s`; `Gancho_com_tempo_antigo_e_religado` (settings com 75 → `EstaLigado` falso; ligar de novo deixa uma entrada só, com 330); `Espera_da_pergunta_cabe_no_gancho` (`EsperaPerguntaMs < 330_000` e `> 300_000`).
- [ ] **Step 2:** Rodar; falham.
- [ ] **Step 3:** Implementar; no `Mensageiro`, a espera do canal e o prazo escolhem pelo `pedido.Perguntas is not null`.
- [ ] **Step 4:** Rodar; verdes.
- [ ] **Step 5:** Commit `feat: tempo das perguntas no gancho`.

### Task 3: Modo de pergunta no notch (Ui)

**Files:** `src/Vitral.Ui/Barra/EstadoNotch.cs` (e um parcial `EstadoNotch.Perguntas.cs`); teste `tests/Vitral.Ui.Tests/NotchPerguntasTests.cs`; ajustar `NotchTests` ao `Encerrado` novo.

**Interfaces (Consumes):** Task 1. **(Produces):**
- `event Action<PedidoPermissao, RespostaPedido?> Encerrado`
- `const double TempoPergunta = 300`; o tempo de cada pedido é `TempoPergunta` com `Perguntas` que cabem, senão `TempoPedido`.
- `int PerguntaAtual` (índice), `IReadOnlySet<string> Marcadas`, `bool PodeEnviar`
- `bool Escolheu(string id, string rotulo)` — escolha única: guarda e avança; na última, encerra com as respostas.
- `void Marcar(string id, string rotulo)` — liga/desliga numa pergunta múltipla.
- `bool Enviar(string id)` — múltipla: guarda as marcadas (`RegrasPerguntas.Juntar`) e avança ou encerra.
- `Respondeu(id, EscolhaPermissao.Conversa)` encerra com `Conversa`.
- Os três respeitam `EsperaClique` e o id da ficha, como `Respondeu`.

- [ ] **Step 1: Testes:** `Escolha_unica_responde`; `Duas_perguntas_acumulam` (Encerrado só depois da segunda, com as duas); `Multipla_marca_desmarca_e_envia` (`PodeEnviar` falso sem marcadas); `Pergunta_dura_5_minutos` (aos 61 s continua; aos 300 s sai sem resposta); `Pergunta_respondida_no_app_sai` (`Desistiu` → sai, `PedidoPerdido` falso); `Conversa_encerra_com_conversa`; `Clique_logo_ao_aparecer_e_ignorado`.
- [ ] **Step 2:** Rodar; falham.
- [ ] **Step 3:** Implementar; as respostas parciais e as marcadas zeram quando o pedido sai da ficha.
- [ ] **Step 4:** Rodar; verdes, suíte inteira.
- [ ] **Step 5:** Commit `feat: modo de pergunta no notch`.

### Task 4: Ficha de pergunta e o canal (App)

**Files:** `src/Vitral.App/Barra/JanelaNotch.cs`, `src/Vitral.App/Claude/CanalAvisos.cs`, `src/Vitral.App/Barra/ControleBarra.cs` (o tipo da espera), `src/Vitral.App/App.xaml.cs` se o canal for montado lá.

- [ ] **Step 1:** `Pedir` devolve `Task<RespostaPedido?>`; `CanalAvisos` escreve `RespostaPermissao.Linha(id, resposta)`.
- [ ] **Step 2:** Ficha de pergunta (quando `PedidoAtual.Perguntas` cabe): cabeçalho "Claude pergunta" e a sessão, o texto (2 linhas, inteiro na dica), "1/2" com várias, um botão por opção (descrição na dica; marcado com o destaque na múltipla), "Enviar" na múltipla, "Responder na conversa" sempre. Não cabe: cabeçalho, primeira pergunta e "Responder na conversa". A barra de tempo usa o tempo do pedido. A confirmação mostra "Respondido" / "Na conversa".
- [ ] **Step 3:** "Responder na conversa" traz o app do Claude para a frente pelo mesmo caminho do clique no aviso do Claude (a conversa da sessão quando conhecida).
- [ ] **Step 4:** Build sem avisos; suíte verde; na máquina: religar os ganchos, uma pergunta real do Claude respondida pelo notch neste app (escolha única e múltipla), uma respondida no app (a ficha some) e "Responder na conversa".
- [ ] **Step 5:** Commit `feat: ficha de pergunta no notch`.

### Task 5: Passada final com o Gustavo e notas

- [ ] Gustavo testa; notas da versão em `docs/notas/`; ideias pendentes atualizadas.
