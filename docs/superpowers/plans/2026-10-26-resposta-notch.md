# Ler e responder ao Claude pelo notch — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** quando uma sessão do Claude termina fora do app, o notch mostra a resposta inteira e aceita uma resposta por
texto. O que ficar sem resposta vai para uma lista de pendências.

**Architecture:** o gancho `Stop` passa a segurar a sessão quando a janela do Claude não está na frente. O mensageiro
manda um `PedidoResposta` pelo canal de mão dupla que já existe e devolve `decision: block` com o texto. No Vitral, o
`EstadoNotch` ganha o modo `Resposta`, e uma classe `Pendencias` guarda o que ficou sem resposta. A janela do notch
ganha a ficha B e a lista desce como painel.

**Tech Stack:** .NET 10, WPF, xUnit. Build e testes com `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet test --nologo -v q'`.

**Spec:** `docs/superpowers/specs/2026-10-26-resposta-notch-design.md`

## Global Constraints

- Timeout do item `Stop` no settings.json: `1200`. `PermissionRequest` continua `75`, e `Notification` fica sem timeout.
- `Canal.EsperaRespostaMs = 1_140_000`. O prazo total do `--aviso` no `Stop` com ficha é de 19,5 min (1.170.000 ms);
  sem ficha continua 1,5 s.
- `TempoResposta = 60` s desde a chegada; `TetoDigitando = 15` min desde o primeiro caractere; confirmação "✓ Enviado"
  por 1 s; `EsperaClique = 0,5` s (o mesmo das permissões).
- `AvisoClaude.MaximoLinha = 65536`. `Completo` vai até 32.768 caracteres, cortado com "…". O trecho da transcrição
  são os últimos 262.144 bytes.
- Linha de pedido `{"tipo":"Resposta",...}`; resposta `{"id":"…","texto":"…"}`; saída `{"decision":"block","reason":texto}`.
- Textos fixos: "Claude terminou", "ver tudo ⌄", "abrir no Claude ›", "Enviar", "✓ Enviado", "Pendências",
  "Abrir no Claude", "Descartar", "terminou", "esperando você", placeholder
  "Responder ao Claude… (Enter envia, Esc cancela)".
- Cores e forma: as da ficha de permissão (botão principal #F2F2F2 com texto #111; secundário com contorno #47FFFFFF;
  barra #E6E6E6, cinza #9CA0AA quando parada). Largura máxima da ficha: 460 px.

## Review Focus

1. **Foco do teclado:** depois de enviar ou cancelar, o foco tem de voltar à janela onde o Gustavo estava, e o notch
   volta a não pegar foco. Conferência na Task 5, com o bloco de notas.
2. **Permissão chegando no meio da digitação:** a ficha de resposta sai da frente, e o texto digitado volta junto quando
   ela retorna. Teste `Permissao_passa_a_frente_e_a_resposta_volta_com_o_texto` (Task 4).
3. **Janela do Claude na frente:** a sessão não fica presa. Teste `Nao_segura_com_o_Claude_na_frente` (Task 3) e
   conferência na Task 6.
4. **Texto com aspas, barras e quebras de linha:** chega ao Claude igual ao digitado. Teste
   `ParaClaude_preserva_aspas_e_quebras` (Task 2).
5. **Notch invisível** (barra desligada, tela cheia): a sessão não fica presa 19 min e a conversa entra nas
   pendências. Teste `Sem_notch_visivel_vai_direto_para_as_pendencias` (Task 4), mais o retorno imediato no
   `ControleBarra.Responder` (Task 5).

---

### Task 1: Gancho Stop com timeout 1200

**Files:**
- Modify: `src/Vitral.Core/Claude/GanchosClaude.cs`
- Test: `tests/Vitral.Core.Tests/GanchosClaudeTests.cs`

**Interfaces:**
- Produces: `GanchosClaude.TempoResposta = 1200` (const, public); `EstaLigado` exige o `Stop` nosso com `timeout` 1200.

- [ ] **Step 1: testes**

```csharp
[Fact] public void Ligar_poe_timeout_1200_no_Stop()  // o item nosso do Stop tem "timeout": 1200; PermissionRequest 75; Notification sem timeout
[Fact] public void Stop_sem_o_timeout_novo_aparece_desligado() // settings da 0.8.x (Stop sem timeout) → EstaLigado false
[Fact] public void Ligar_de_novo_nao_duplica() // Ligar sobre o settings da 0.8.x → um item nosso por evento
```

- [ ] **Step 2:** rodar → FAIL (sem timeout no Stop).
- [ ] **Step 3:** `Ligar` escreve `TempoResposta` no `Stop`; `EstaLigado` confere o timeout do item nosso do `Stop`.
- [ ] **Step 4:** rodar → PASS, suíte inteira verde.
- [ ] **Step 5:** commit `feat(core): gancho Stop com tempo para a resposta`.

### Task 2: Mensagem inteira, pedido de resposta e resposta (Core)

**Files:**
- Modify: `src/Vitral.Core/Claude/AvisoClaude.cs`
- Create: `src/Vitral.Core/Claude/PedidoResposta.cs`
- Modify: `src/Vitral.Core/Claude/Canal.cs`
- Test: `tests/Vitral.Core.Tests/AvisoClaudeTests.cs`, `tests/Vitral.Core.Tests/RespostaTests.cs`

**Interfaces:**
- Produces:
  - `AvisoClaude.MaximoLinha = 65536`; propriedades `string? Completo { get; init; }`, `bool ForaDoApp { get; init; }`
    na linha;
  - `static string AvisoClaude.Completar(string texto)`: tira o Markdown simples (o mesmo do `Encurtar`), mantém as
    quebras de parágrafo (linha em branco), junta espaços dentro da linha, corta em 32.768 com "…";
  - `AvisoClaude.DeGancho(json, lerFimDoArquivo, bool foraDoApp)`: preenche `Completo` (no `Stop` e no `Notification`,
    a partir da transcrição) e `ForaDoApp`;
  - `sealed record PedidoResposta(string Id, string Sessao, string Completo, DateTime Hora) { string? SessaoId { get; init; } }`
    com `static PedidoResposta? DeAviso(AvisoClaude aviso, DateTime hora)` (id novo), `ParaLinha()`,
    `static PedidoResposta? DeLinha(string?)`, `static bool EhPedido(string?)`;
  - `static class RespostaTexto { string Linha(string id, string texto); string? DaLinha(string? linha, string idEsperado); string ParaClaude(string texto); }`;
  - `Canal.EsperaRespostaMs = 1_140_000`.

- [ ] **Step 1: testes**

```csharp
[Fact] public void Completar_tira_markdown_e_mantem_paragrafos()
    // "# Pronto\n\nUse **isto**:\n- a\n- b" → "Pronto\n\nUse isto:\na\nb"
[Fact] public void Completar_corta_em_32k() // 40.000 'x' → 32.768 caracteres, termina em "…"
[Fact] public void Stop_traz_o_completo_e_o_fora_do_app()
    // DeGancho(Stop com transcript, foraDoApp: true) → Completo = resposta inteira; ForaDoApp true
[Fact] public void Linha_de_64k_passa_e_maior_nao()
[Fact] public void Pedido_de_resposta_ida_e_volta() // DeLinha(ParaLinha()) igual, com SessaoId; EhPedido true; PedidoPermissao.EhPedido false
[Fact] public void Resposta_com_id_errado_ou_vazia_e_null()
[Fact] public void ParaClaude_preserva_aspas_e_quebras()
    // texto "faz a \"B\"\nC:\\x" → JSON com decision "block" e reason igual ao texto
```

- [ ] **Step 2:** rodar → FAIL (tipos novos).
- [ ] **Step 3:** implementar como nas interfaces. O `Completo` não entra no `Encurtar`: o resumo de 80 caracteres
  continua saindo do texto original.
- [ ] **Step 4:** rodar → PASS, suíte inteira verde.
- [ ] **Step 5:** commit `feat(core): mensagem inteira e pedido de resposta do Claude`.

### Task 3: Mensageiro segura o Stop fora do app

**Files:**
- Create: `src/Vitral.Core/Claude/Segurar.cs`
- Modify: `src/Vitral.App/Claude/Mensageiro.cs`, `src/Vitral.App/Program.cs`
- Test: `tests/Vitral.Core.Tests/RespostaTests.cs`

**Interfaces:**
- Consumes: Task 2 inteira.
- Produces: `static bool Segurar.NoStop(string entrada, bool claudeNaFrente)` (true só em `Stop` com o Claude fora da
  frente); `Mensageiro.ClaudeNaFrente()` (janela da frente pertence a um processo `claude`);
  `Mensageiro.PrazoMs(string entrada)` → 65.000 (permissão), 1.170.000 (Stop a segurar) ou 1.500.

- [ ] **Step 1: testes**

```csharp
[Fact] public void Nao_segura_com_o_Claude_na_frente() => Assert.False(Segurar.NoStop(stop, claudeNaFrente: true));
[Fact] public void Segura_o_Stop_fora_do_app() => Assert.True(Segurar.NoStop(stop, claudeNaFrente: false));
[Fact] public void Notification_nunca_segura() => Assert.False(Segurar.NoStop(notification, claudeNaFrente: false));
```

- [ ] **Step 2:** rodar → FAIL.
- [ ] **Step 3:** implementar.
  - `Mensageiro.Executar`: lê o fim da transcrição com o limite de 256 KB. Se `Segurar.NoStop`, monta o
    `PedidoResposta` a partir do aviso e chama `Canal.Perguntar(nome, linha, Canal.EsperaRespostaMs)`; com texto,
    escreve `RespostaTexto.ParaClaude`.
  - Senão, manda o aviso com `ForaDoApp = !ClaudeNaFrente()`.
  - `Program` usa `Mensageiro.PrazoMs`, decidido antes de rodar e com a mesma leitura do `ClaudeNaFrente`.
- [ ] **Step 4:** rodar → PASS.
- [ ] **Step 5:** conferência com o script de simulação (Stop com `transcript_path` de verdade).
  - Com o Vitral desta branch aberto e o Claude atrás: o mensageiro fica esperando.
  - Com o Vitral fechado: sai em menos de 1 s, sem saída.
- [ ] **Step 6:** commit `feat: o gancho Stop segura a sessão fora do app`.

### Task 4: Regra da ficha de resposta e das pendências (Ui)

**Files:**
- Modify: `src/Vitral.Ui/Barra/EstadoNotch.cs`
- Create: `src/Vitral.Ui/Barra/Pendencias.cs`
- Test: `tests/Vitral.Ui.Tests/RespostaNotchTests.cs`

**Interfaces:**
- Consumes: `PedidoResposta` (Task 2).
- Produces:
  - `ModoNotch.Resposta`; constantes `EstadoNotch.TempoResposta = 60`, `TetoDigitando = 900`;
  - `void PediuResposta(PedidoResposta p)`;
  - `PedidoResposta? RespostaAtual`;
  - `string Rascunho` (texto guardado);
  - `void Digitou(string id, string texto)`: o primeiro caractere para o tempo;
  - `bool Enviou(string id)`: usa o `Rascunho`; respeita `EsperaClique`; recusa texto vazio;
  - `void Cancelou(string id)`;
  - `void DesistiuResposta(string id)`;
  - `event Action<PedidoResposta, string?> RespostaEncerrada`;
  - `bool Digitando`;
  - `DateTime? ChegadaDaResposta`;
  - `Pendencias Pendencias { get; }`;
  - `sealed class Pendencias`:
    - `record Pendencia(string? SessaoId, string Sessao, TipoAviso Tipo, DateTime Hora, string Completo)`;
    - `IReadOnlyList<Pendencia> Itens` (mais nova primeiro);
    - `int Quantas`;
    - `void Entrou(Pendencia p)` (substitui a da mesma sessão);
    - `void Saiu(Pendencia p)`;
    - `void SaiuDaSessao(string? sessaoId, string sessao)`;
    - `event Action? Mudou`.

**Regras** (spec §4.1):
- **Prioridade:** permissão > resposta > aviso.
- **Prazo:** 60 s desde a chegada. Se a ficha estava digitando, o teto de 15 min conta do primeiro caractere.
- **Sem texto:** tempo, teto ou Esc → `Pendencias.Entrou(...Terminou...)`. O aviso `Esperando` com `ForaDoApp` também
  entra.
- **Aviso ou pedido novo da mesma sessão:** `Pendencias.SaiuDaSessao`; um aviso da mesma sessão descarta a ficha dela
  (sem entrar nas pendências).

- [ ] **Step 1: testes**

```csharp
[Fact] public void Pedido_de_resposta_abre_a_ficha()
[Fact] public void Sessenta_segundos_sem_digitar_encerra_sem_texto_e_vai_para_as_pendencias()
[Fact] public void Digitar_para_o_tempo_e_vale_o_teto_de_15_minutos() // Avancar(60.1) ainda Resposta; Avancar(900) depois do 1º caractere → encerra sem texto
[Fact] public void Enviar_encerra_com_o_texto_e_confirma() // RespostaEncerrada(p, "pode seguir"); Confirmando; 1 s depois Normal
[Fact] public void Enviar_vazio_e_recusado()
[Fact] public void Esc_cancela_e_vai_para_as_pendencias()
[Fact] public void Desistir_tira_sem_ir_para_as_pendencias()
[Fact] public void Aviso_da_mesma_sessao_descarta_a_ficha()
[Fact] public void Fila_de_respostas_com_mais_um()
[Fact] public void Permissao_passa_a_frente_e_a_resposta_volta_com_o_texto()
[Fact] public void Esperando_fora_do_app_entra_nas_pendencias()
[Fact] public void Pendencia_nova_da_mesma_sessao_substitui_a_antiga()
[Fact] public void Sem_notch_visivel_vai_direto_para_as_pendencias() // EstadoNotch.SemNotch(p): Pendencias.Quantas 1, nenhuma ficha
```

- [ ] **Step 2:** rodar → FAIL.
- [ ] **Step 3:** implementar no `EstadoNotch`, no molde da fila de permissões: `Mostrar`, `_mostrou`, a confirmação
  de 1 s. Mais `void SemNotch(PedidoResposta p)`, que só registra a pendência.
- [ ] **Step 4:** rodar → PASS, suíte inteira verde (os testes de permissão continuam passando).
- [ ] **Step 5:** commit `feat(ui): ficha de resposta e pendências no estado do notch`.

### Task 5: Canal, ficha B e lista de pendências (App)

**Files:**
- Modify: `src/Vitral.App/Claude/CanalAvisos.cs`, `src/Vitral.App/App.xaml.cs`, `src/Vitral.App/Barra/ControleBarra.cs`,
  `src/Vitral.App/Barra/JanelaNotch.cs`
- Create: `src/Vitral.App/Barra/PainelPendencias.cs`
- Modify: `src/Vitral.Ui/Barra/CoordenadorPaineis.cs` (`Painel.Pendencias`)

**Interfaces:**
- Consumes: Tasks 2 e 4.
- Produces:
  - `CanalAvisos(..., Func<PedidoResposta, Task<string?>> chegouResposta)`;
  - `ControleBarra.Responder(PedidoResposta p) : Task<string?>`. Sem notch visível, chama `SemNotch` e devolve `null`
    na hora;
  - `JanelaNotch.Responder(PedidoResposta p) : Task<string?>`, `JanelaNotch.DesistirResposta(string id)`.

- [ ] **Step 1: canal.**
  - O `CanalAvisos.Atender` trata `PedidoResposta.EhPedido` como o pedido de permissão: decisão ou fechamento, com
    `WaitAsync` de 19,5 min; com texto, escreve `RespostaTexto.Linha`.
  - O buffer do `LerLinha` sobe para `MaximoLinha * 4 + 1`, já que `MaximoLinha` passa a 64 KB; o servidor do canal usa
    buffers de 64 KB.
- [ ] **Step 2: ficha B** no `JanelaNotch` (spec §4.2):
  - topo com símbolo, "Claude terminou", "pasta · hora" e "abrir no Claude ›";
  - mensagem com duas linhas e esmaecido, "ver tudo ⌄", e rolar o mouse abre até 150 px;
  - caixa de texto (`TextBox` com `AcceptsReturn`; Enter envia, Shift+Enter quebra linha, Esc cancela), "Enviar"
    desligado com a caixa vazia;
  - barra de 60 s que para e fica cinza ao digitar;
  - "✓ Enviado" por 1 s.
- [ ] **Step 3: foco.**
  - `PreviewMouseDown` na caixa: guarda `GetForegroundWindow()`, tira o `WS_EX_NOACTIVATE` e chama `Activate()`.
  - Ao encerrar a ficha: devolve o `WS_EX_NOACTIVATE` e `SetForegroundWindow` à janela guardada, se ela ainda existir.
- [ ] **Step 4: lateral e lista.**
  - Com `Pendencias.Quantas > 0`, a lateral esquerda mostra "Pendências" e o número; o clique abre `Painel.Pendencias`,
    centrado no notch.
  - "Pedido sem resposta" continua com prioridade na lateral.
  - O `PainelPendencias` tem itens como em `notch-resposta.html`: o clique expande a mensagem (até 110 px) com "Abrir no
    Claude" (`AbrirClaude.Trazer` + `Saiu`) e "Descartar"; "×" tira direto.
- [ ] **Step 5:** build sem avisos; suíte verde.
- [ ] **Step 6: conferência com o Gustavo,** com pedidos simulados (script com `PedidoResposta` direto pelo `--aviso` de
  um `Stop`):
  - a ficha B;
  - "ver tudo";
  - digitar com o bloco de notas aberto atrás: ao enviar, o cursor volta ao bloco de notas;
  - Esc;
  - os 60 s levando à lista;
  - "Abrir no Claude" e "Descartar".
- [ ] **Step 7:** commit `feat: ficha de resposta e lista de pendências no notch`.

### Task 6: Teste real com o Claude

- [ ] **Step 1:** o Gustavo desmarca e marca "Avisos do Claude no notch"; conferir `timeout` 1200 no `Stop` do
  settings.json.
- [ ] **Step 2:** sessão real com o app em segundo plano: responder pelo notch; o Claude continua com o texto.
- [ ] **Step 3:** deixar passar os 60 s → pendência → abrir pelo item.
- [ ] **Step 4:** com o app do Claude na frente, a sessão termina e não fica presa.
- [ ] **Step 5:** duas sessões terminando juntas → fila; e uma mensagem digitada no app enquanto o notch segura (anotar
  o comportamento no ledger, spec §5).
- [ ] **Step 6:** commit de eventuais ajustes, com teste primeiro.

### Task 7: Fechamento e 0.9.0

- [ ] **Step 1:** suítes completas → PASS; build sem avisos.
- [ ] **Step 2:** revisão final por um agente Opus com o Review Focus acima. Corrigir os itens Crítico e Importante com
  teste primeiro.
- [ ] **Step 3:** com o ok do Gustavo:
  - versão `0.9.0` e `docs/notas/0.9.0.md`;
  - juntar na main, enviar ao GitHub e `build.ps1 -Versao 0.9.0 -Publicar`;
  - instalar neste PC;
  - o Gustavo marca a caixa de novo no Vitral instalado;
  - atualizar `docs/ideias-pendentes.md`.
