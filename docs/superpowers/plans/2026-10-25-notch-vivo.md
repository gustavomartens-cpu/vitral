# Notch vivo — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** o notch do relógio ganha orelhas e passa a mostrar os avisos do Claude Code (terminou / esperando você), levando ao Claude com um clique.

**Architecture:**

- **Contas e regras puras:**
  - `AvisoClaude` e `GanchosClaude`, no Core;
  - `FormaNotch` e `EstadoNotch`, no Ui.

  Todas têm testes.
- **App:**
  - o mensageiro `Vitral.exe --aviso`, no `Program.Main`, antes do Velopack;
  - o servidor do *named pipe* (`CanalAvisos`);
  - a janela transparente `JanelaNotch` no lugar da ilha do relógio;
  - a caixa na janela principal que liga e desliga os ganchos no `settings.json` do Claude Code.

**Tech Stack:** .NET 10, WPF, System.Text.Json (JsonNode), System.IO.Pipes, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-25-notch-vivo-design.md`

## Global Constraints

- O mensageiro **nunca** falha para o Claude: o código de saída é sempre 0, toda exceção é engolida e ele espera no máximo 2 s pela entrada e 300 ms pela conexão (spec §4.3).
- O canal é só do usuário atual (`PipeSecurity` com o SID), com linha de no máximo 4 KB (spec §4.4).
- O `settings.json` do Claude é alterado só nos nossos itens: há uma cópia `.vitral-bak` antes de gravar, a troca é feita por arquivo temporário, e um JSON com defeito nunca é gravado (spec §4.2).
- Medidas do notch: normal 34 px (raio 16), aviso 52 px até 360 px (raio 22), orelhas de 16 px, cor `#FF0B0C10`, aviso de 6 s e transição de 320 ms com `CurvaMola` (spec §3).
- As animações passam pela chave geral do tema (`Efeitos.Tempo`/`Movimento.Ligado`).
- Textos e comentários em português, no estilo do código vizinho; `TreatWarningsAsErrors`; parar o `Vitral` antes de compilar o App.
- Teste: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet test --nologo -v q'`.

## Review Focus

1. **Claude rodando com o Vitral fechado, travado ou lento:** o gancho sai em menos de 0,5 s com código 0, e o Claude segue normal. Teste `Sem_servidor_sai_rapido` (Task 4) e medida.
2. **`settings.json` com outros ganchos, comentários do usuário ou formato inesperado:** ligar e desligar preservam tudo o que não é nosso. Um arquivo com defeito não é tocado e mostra o aviso. Testes da Task 2.
3. **Transcrição enorme, com linha cortada ou só com uso de ferramentas no fim:** o texto vem da última mensagem do assistente que tem texto, ou fica vazio, sem lançar exceção. Testes da Task 1.
4. **Avisos em rajada:** vários avisos seguidos, inclusive com o mouse sobre o notch. O último vale, e o notch não fica preso aberto. Testes da Task 3.
5. **Barra do topo desligada, tela cheia ou troca de monitor/escala:** o aviso é descartado ou o notch some junto com as ilhas, e as orelhas continuam emendadas na borda depois de mudar a escala. Conferência na Task 5.

## Desvio anotado da spec

O §4.6 da spec pedia `PrincipalViewModel.AvisosClaude`. A caixa vizinha, "Iniciar com o Windows", é tratada no code-behind da `JanelaPrincipal` (`IniciarComWindows_Mudou`), e a caixa nova segue o mesmo padrão. A lógica que precisa de teste fica no `GanchosClaude` (Task 2). Custo se errado: o code-behind da caixa fica sem teste automático.

---

### Task 1: Aviso a partir do gancho

**Files:**
- Create: `src/Vitral.Core/Claude/AvisoClaude.cs`
- Test: `tests/Vitral.Core.Tests/AvisoClaudeTests.cs`

**Interfaces:**
- Produces (namespace `Vitral.Core.Claude`):
  - `public enum TipoAviso { Terminou, Esperando }`
  - `public sealed record AvisoClaude(TipoAviso Tipo, string Sessao, string Texto)` com:
    - `static AvisoClaude? DeGancho(string json, Func<string, string?> lerFimDoArquivo)`: o delegate recebe o `transcript_path` e devolve os últimos 64 KB, ou null;
    - `static string UltimaRespostaDoAssistente(string trechoJsonl)`: a última linha válida com `"type":"assistant"` cujo `message.content` tem um bloco `{"type":"text","text":...}`, pegando o primeiro desses blocos; senão `""`. Linha que não é JSON é ignorada (a primeira do trecho pode vir cortada);
    - `static string Encurtar(string texto, int maximo = 80)`: junta os espaços e as quebras de linha, tira `*`, `` ` `` e os `#` do começo, corta em `maximo` com "…" (o "…" conta no limite);
    - `string ParaLinha()` e `static AvisoClaude? DeLinha(string linha)`: o JSON do canal, `{"tipo":"Terminou","sessao":"…","texto":"…"}`. Linha inválida ou com mais de 4096 caracteres devolve null.

- [ ] **Step 1: Testes que falham** (`AvisoClaudeTests`):
  - `Stop_vira_terminou_com_a_ultima_resposta`:
    - entrada `{"hook_event_name":"Stop","cwd":"G:\\dev\\vitral","transcript_path":"t"}`;
    - o delegate devolve duas linhas JSONL, a última do assistente com o texto "Pronto, fiz o commit.";
    - resultado `Terminou`, sessão "vitral", texto "Pronto, fiz o commit.".
  - `Notification_vira_esperando_com_a_mensagem`: entrada `{"hook_event_name":"Notification","cwd":"C:\\x\\sic","message":"Claude needs your permission to use Bash"}` → `Esperando`, sessão "sic" e a mensagem.
  - `Evento_desconhecido_ou_json_ruim_e_null`: `{"hook_event_name":"PreToolUse"}`, `""` e `"{x"` devolvem null.
  - `Sem_cwd_a_sessao_e_Claude`.
  - `Transcricao_ilegivel_deixa_texto_vazio`: o delegate devolve null.
  - `Ultima_resposta_pula_mensagens_so_de_ferramenta`: a última linha do assistente só tem `tool_use`, a anterior tem texto → devolve o texto da anterior.
  - `Ultima_resposta_ignora_linha_cortada`: o trecho começa com `ssistant","message":{` (cortado) → não lança e devolve o texto da linha válida.
  - `Encurtar_junta_linhas_tira_markdown_e_corta`:
    - `"**Feito**\n\n`codigo` ok"` vira `"Feito codigo ok"`;
    - um texto de 100 caracteres sai com 80, terminando em "…".
  - `Linha_do_canal_ida_e_volta`; `Linha_maior_que_4k_e_null`.
- [ ] **Step 2: Rodar** `dotnet test tests/Vitral.Core.Tests` → FAIL (os tipos não existem).
- [ ] **Step 3: Implementar** com `System.Text.Json` (`JsonDocument`); a sessão é `Path.GetFileName(cwd.TrimEnd('\\','/'))`.
- [ ] **Step 4: Rodar** → PASS.
- [ ] **Step 5: Commit** `feat(core): aviso do Claude a partir do gancho`.

---

### Task 2: Ganchos no settings.json

**Files:**
- Create: `src/Vitral.Core/Claude/GanchosClaude.cs`
- Test: `tests/Vitral.Core.Tests/GanchosClaudeTests.cs`

**Interfaces:**
- Produces (`Vitral.Core.Claude.GanchosClaude`, estática):
  - `const string Marca = "Vitral.exe\" --aviso"`
  - `static string Comando(string caminhoExe)` = `$"\"{caminhoExe}\" --aviso"`
  - `static bool EstaLigado(string texto)`
  - `static string Ligar(string texto, string comando)`
  - `static string Desligar(string texto)`
  - **Formato de cada item:** `{"hooks":[{"type":"command","command":comando}]}` em `hooks.Stop` e `hooks.Notification`. Um item é "nosso" se algum `command` contém `Marca`.
  - **Implementação:** `JsonNode.Parse` com `JsonDocumentOptions { CommentHandling = Skip, AllowTrailingCommas = true }` e saída indentada. Texto vazio ou só espaços conta como `{}`. JSON com defeito ou raiz que não é objeto lança `FormatException`.

- [ ] **Step 1: Testes que falham** (`GanchosClaudeTests`):
  - `Ligar_em_arquivo_vazio_cria_os_dois_ganchos`: `EstaLigado` true; `Stop` e `Notification` com um item cada.
  - `Ligar_preserva_outras_chaves_e_ganchos`: o arquivo tem `"model":"x"` e um `hooks.Stop` alheio (`"command":"echo oi"`) → ambos continuam, e o nosso é acrescentado.
  - `Ligar_duas_vezes_nao_duplica`.
  - `Ligar_com_caminho_novo_troca_o_antigo`: continua com um item nosso, agora com o caminho novo.
  - `Desligar_tira_so_os_nossos_e_limpa_vazios`:
    - com o alheio presente, ele fica;
    - num arquivo só com os nossos, some a chave `hooks` inteira.
  - `Desligar_quando_desligado_nao_muda_nada`: o texto equivale ao original (comparação por `JsonNode.DeepEquals`).
  - `Json_com_defeito_lanca_FormatException`: `"{\"hooks\": ["` lança, tanto em `Ligar` quanto em `Desligar`.
- [ ] **Step 2: Rodar** → FAIL. **Step 3: Implementar.** **Step 4: Rodar** → PASS.
- [ ] **Step 5: Commit** `feat(core): ganchos do Claude no settings.json`.

---

### Task 3: Forma e estados do notch

**Files:**
- Create: `src/Vitral.Ui/Barra/FormaNotch.cs`, `src/Vitral.Ui/Barra/EstadoNotch.cs`
- Test: `tests/Vitral.Ui.Tests/NotchTests.cs`

**Interfaces:**
- Consumes: `Vitral.Core.Claude.AvisoClaude` (Task 1; o Ui já referencia o Core).
- Produces:
  - `FormaNotch`:
    - `const double Orelha = 16`;
    - `static string Caminho(double largura, double altura, double raio, double orelha = Orelha)`: minilinguagem de `Path`. A origem é o topo da orelha esquerda e a largura total é `largura + 2×orelha`. O caminho vai pela borda de cima, desce a orelha direita como arco côncavo, faz o canto de baixo direito com `raio`, a base e o canto de baixo esquerdo, sobe a orelha esquerda e fecha. Números com `CultureInfo.InvariantCulture`.
  - `enum ModoNotch { Normal, Aviso, Lembrete }`
  - `enum AcaoNotch { Nenhuma, AbrirClaude, AbrirCalendario }`
  - `sealed class EstadoNotch`, com relógio injetado (`Func<DateTime> agora`):
    - propriedades `Modo` e `Aviso` (`AvisoClaude?`);
    - evento `Mudou`;
    - métodos:
      - `Chegou(AvisoClaude a)`: vai para `Aviso` e reinicia os 6 s;
      - `Tique()`: chamado a cada 250 ms; passados 6 s sem o mouse em cima, vai para `Lembrete`;
      - `MouseEntrou()`: segura o tempo;
      - `MouseSaiu()`: recomeça a contar os 6 s a partir de agora;
      - `AcaoNotch Clicou()`:
        - em `Aviso`, vai para `Normal` e devolve `AbrirClaude`;
        - em `Lembrete` ou `Normal`, vai para `Normal` e devolve `AbrirCalendario`;
    - `const double Duracao = 6` (segundos).
  - `NotchMedidas`:
    - `AlturaNormal 34`, `AlturaAviso 52`, `LarguraMaxAviso 360`, `RaioNormal 16`, `RaioAviso 22`, `ExtraLembrete 14`;
    - `JanelaLargura = LarguraMaxAviso + 2×Orelha`;
    - `JanelaAltura = PosicaoIlhas.NotchSobra + AlturaAviso`.

- [ ] **Step 1: Testes que falham** (`NotchTests`):
  - `Caminho_comeca_no_topo_e_fecha`: começa com `"M0,0"` e termina com `"Z"`; contém `"A16,16"` (as orelhas) e `"A22,22"` quando o raio é 22.
  - `Caminho_tem_a_largura_total`: para largura 200, algum ponto tem X = 232, e nenhum X passa de 232.
  - `Aviso_depois_de_6s_vira_lembrete`: relógio falso; `Chegou`, avança 5,9 s com `Tique`, ainda `Aviso`; avança até 6,1 s, vira `Lembrete`.
  - `Clique_no_aviso_abre_o_Claude_e_volta_ao_normal`.
  - `Aviso_novo_reinicia_o_tempo`: `Chegou` em t=0 e em t=5; em t=10, ainda `Aviso`, com o segundo aviso.
  - `Mouse_em_cima_segura_o_aviso`: `MouseEntrou` em t=1; em t=20, ainda `Aviso`; `MouseSaiu` em t=20; em t=26,1, `Lembrete`.
  - `Clique_no_lembrete_abre_o_calendario_e_limpa`.
  - `Mudou_dispara_em_cada_troca_de_modo`.
- [ ] **Step 2: Rodar** → FAIL. **Step 3: Implementar.** **Step 4: Rodar** → PASS.
- [ ] **Step 5: Commit** `feat(ui): forma e estados do notch`.

---

### Task 4: Mensageiro e canal

**Files:**
- Create: `src/Vitral.App/Claude/Mensageiro.cs`, `src/Vitral.App/Claude/CanalAvisos.cs`
- Modify: `src/Vitral.App/Program.cs`, `src/Vitral.App/App.xaml.cs`
- Test: `tests/Vitral.Core.Tests/CanalTests.cs` (o nome do canal e o protocolo; o envio, com um servidor de teste)

**Interfaces:**
- Consumes: Task 1 (`AvisoClaude.DeGancho`, `ParaLinha`, `DeLinha`).
- Produces:
  - `Vitral.Core.Claude.Canal`:
    - `static string Nome(string sid)` = `$"Vitral-Avisos-{sid}"`;
    - `static bool Enviar(string nomeCanal, string linha, int esperaMs = 300)`: conecta como cliente, escreve a linha em UTF-8 com `\n` e devolve false sem lançar em qualquer falha.

    Fica no Core, para ter teste e ser usado pelo App.
  - **`Mensageiro.Executar()`, no App:**
    - lê a entrada padrão (até 64 KB, em 2 s, numa tarefa com timeout);
    - o `lerFimDoArquivo` lê os últimos 64 KB do arquivo;
    - chama `DeGancho` e, se houver aviso, `Canal.Enviar(Canal.Nome(sidAtual), aviso.ParaLinha())`;
    - tudo dentro de `try/catch`.
  - **`Program.Main`:** a primeira linha passa a ser `if (args is ["--aviso", ..]) { Claude.Mensageiro.Executar(); return; }`, antes do `VelopackApp`.
  - **`CanalAvisos` (App, `IDisposable`):**
    - construtor `CanalAvisos(Dispatcher ui, Action<AvisoClaude> chegou)`;
    - numa tarefa, faz o laço com `NamedPipeServerStream` (In, 1 instância, `PipeOptions.Asynchronous`, `PipeSecurity` com `FullControl` só para o SID atual), espera a conexão, lê uma linha (no máximo 4096 caracteres) e chama `DeLinha`;
    - se vier um aviso, passa para a interface com `ui.BeginInvoke(chegou, aviso)`;
    - `Dispose` cancela o laço.
  - **`App`:** cria o `CanalAvisos` no `OnStartup` (na primeira instância) e o descarta no `Sair()`. Por enquanto, `chegou` grava no `LogBarra.Info` ("Aviso do Claude: …"); a Task 5 liga ao notch.

- [ ] **Step 1: Testes que falham** (`CanalTests`):
  - `Nome_do_canal_tem_o_sid`;
  - `Enviar_chega_ao_servidor`: um `NamedPipeServerStream` de teste com nome aleatório recebe a linha;
  - `Sem_servidor_sai_rapido`: `Enviar` para um nome inexistente devolve false em menos de 1 s.
- [ ] **Step 2: Rodar** → FAIL. **Step 3: Implementar** `Canal` → PASS.
- [ ] **Step 4: Implementar** o `Mensageiro`, o `Program.Main`, o `CanalAvisos` e a ligação no `App`.
- [ ] **Step 5: Build** sem avisos e testes → PASS.
- [ ] **Step 6: Medir e conferir:**
  - com o Vitral de desenvolvimento rodando, mandar 10 vezes um aviso `Notification` para `bin\...\Vitral.exe --aviso` pelo PowerShell e medir com `Measure-Command`. Esperado: no log, "Aviso do Claude: Esperando · sic · Teste"; e a média, anotada no ledger;
  - com o Vitral fechado, o comando sai com código 0 em menos de 0,5 s;
  - média acima de 150 ms: anotar `Ruling:` e abrir a pendência do executável mínimo, sem implementar agora (spec §7).
- [ ] **Step 7: Commit** `feat: mensageiro e canal dos avisos do Claude`.

---

### Task 5: JanelaNotch com orelhas e avisos

**Files:**
- Create: `src/Vitral.App/Barra/JanelaNotch.cs`, `src/Vitral.App/Claude/AbrirClaude.cs`
- Modify: `src/Vitral.App/Barra/ControleBarra.cs` (o relógio passa a usar a `JanelaNotch`), `src/Vitral.App/Barra/IlhaRelogio.xaml(.cs)` (só se precisar expor a largura), `src/Vitral.App/App.xaml.cs` (`chegou` → notch)

**Interfaces:**
- Consumes: Task 3 (`FormaNotch`, `EstadoNotch`, `NotchMedidas`), Task 4 (`CanalAvisos`), `IlhaRelogio` existente.
- Produces:
  - **`JanelaNotch` (Window):**
    - `AllowsTransparency=True`, `WindowStyle=None`, fundo transparente, `Topmost`, `ShowInTaskbar=false`, `ShowActivated=false`; no `SourceInitialized`, os estilos estendidos `WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE`;
    - tamanho fixo `NotchMedidas.JanelaLargura × JanelaAltura`;
    - o `Path` do formato (preenchimento `#FF0B0C10`) é refeito com `FormaNotch.Caminho` a cada quadro de uma animação de três números (largura, altura e raio), animados em 320 ms com a `CurvaMola`. A duração vem de um token novo, `DuracaoNotch` (`0:0:0.32` em `Tokens.xaml`), lido por `Efeitos.Tempo("DuracaoNotch", 320)` e portanto sujeito à chave geral;
    - o conteúdo fica num `Grid` por cima do `Path`: o `IlhaRelogio` no normal e no lembrete, com o ponto âmbar no lembrete; o painel do aviso (spec §3.3) no aviso; a troca faz um fade de `DuracaoMedia`;
    - a largura do corpo no normal é a medida do `IlhaRelogio` + 36 px de folga, como o `Padding` 18,0 de hoje;
    - no aviso, é o conteúdo medido, no máximo 360 px;
    - usa `EstadoNotch` (um `DispatcherTimer` de 250 ms chama `Tique`; `MouseEnter`/`MouseLeave` chamam `MouseEntrou`/`MouseSaiu`);
    - no `MouseLeftButtonUp`, chama `Clicou()`:
      - `AbrirClaude` chama o `AbrirClaude.Trazer()`;
      - `AbrirCalendario` dispara o mesmo `Pediu(Painel.Calendario)` que o relógio já dispara hoje.

      A `JanelaNotch` implementa o `IPedePainel` existente.
    - `public void Avisar(AvisoClaude a)` chama `EstadoNotch.Chegou`.
  - **`AbrirClaude.Trazer()`:** procura um processo `claude` com `MainWindowHandle != 0` e faz `ShowWindow(SW_RESTORE)` se `IsIconic`, depois `SetForegroundWindow`. Sem esse processo, não faz nada.
  - **`ControleBarra`:**
    - o relógio é criado como `JanelaNotch`, e não mais como `JanelaIlha(presaNaBorda: true)`;
    - a posição continua vindo de `PosicaoIlhas.Calcular`, mas o `Left` da janela é ajustado para centrar a janela fixa no centro do relógio, e o `Top` é o mesmo de hoje;
    - novo método `Avisar(AvisoClaude)` que repassa ao notch se ele existir; sem barra do topo, descarta.
  - **`App`:** o `chegou` do `CanalAvisos` chama `_barra?.Avisar(a)`.

- [ ] **Step 1: Implementar** `JanelaNotch`, `AbrirClaude` e a troca no `ControleBarra`/`App`.
- [ ] **Step 2: Build** sem avisos e testes → PASS.
- [ ] **Step 3: Verificação com o Gustavo:**
  - as orelhas emendam na borda (inclusive na escala que ele usa);
  - o relógio e o calendário funcionam como antes;
  - um aviso simulado pelo mensageiro (o comando da Task 4) alarga o notch com mola, fica 6 s, recolhe para o lembrete com o ponto âmbar, e o clique no lembrete abre o calendário;
  - o clique no aviso traz o app Claude para a frente;
  - com o mouse em cima, o aviso não recolhe;
  - em tela cheia, o notch some como as ilhas;
  - com "Efeitos de animação" desligado, a troca é direta.
- [ ] **Step 4: Commit** `feat: notch com orelhas e avisos do Claude`.

---

### Task 5b: Laterais do notch (pedido do Gustavo, spec §3.4)

**Files:**
- Modify: `src/Vitral.Ui/Barra/EstadoNotch.cs`, `src/Vitral.Ui/Barra/FormaNotch.cs` (`NotchMedidas`), `src/Vitral.App/Barra/JanelaNotch.cs`
- Test: `tests/Vitral.Ui.Tests/NotchTests.cs`

**Interfaces:**
- Produces:
  - `EstadoNotch.Laterais` e `EstadoNotch.Pendente` (bool);
  - `AcaoNotch Clicou(ParteNotch parte = ParteNotch.Meio)`;
  - `enum ParteNotch { Meio, Esquerda, Direita }`;
  - `AcaoNotch.AbrirNotificacoes`;
  - `NotchMedidas.LarguraLateral = 130`, `LarguraMaxCorpo = 440`;
  - `JanelaLargura = LarguraMaxCorpo + 2 × Orelha`.

- [ ] **Step 1: Testes que falham:**
  - `Mouse_parado_250ms_abre_as_laterais`: em normal, `MouseEntrou`; com 0,2 s, não; com 0,3 s, `Laterais`; `Mudou` dispara.
  - `Sair_recolhe_as_laterais`.
  - `No_aviso_o_mouse_nao_abre_laterais`.
  - `Clique_na_esquerda_abre_o_Claude_e_limpa_o_lembrete`: no lembrete, `Clicou(Esquerda)` devolve `AbrirClaude`; o modo vai a `Normal` e `Pendente` fica false.
  - `Clique_na_direita_abre_as_notificacoes`.
  - `Pendente_do_aviso_ate_abrir`: `Chegou` deixa true; depois de 6 s (lembrete), continua true; `Clicou()` no aviso deixa false.
- [ ] **Step 2: Rodar** → FAIL. **Step 3: Implementar** → PASS.
- [ ] **Step 4: JanelaNotch:**
  - o corpo com laterais tem a largura do relógio mais 2 × 130;
  - o conteúdo ganha três colunas: lateral esquerda (ícone do Claude, título curto e sessão), relógio, lateral direita (sino e "Notificações"). As laterais entram com o fade de `DuracaoMedia`;
  - o clique em cada coluna chama `Clicou(parte)`;
  - `AbrirNotificacoes` chama `Atalhos.CentralDeNotificacoes()`.
- [ ] **Step 5: Verificação com o Gustavo:** as laterais abrem com o mouse parado e fecham ao sair; a esquerda mostra o aviso perdido e traz o Claude; a direita abre a central; o meio abre o calendário.
- [ ] **Step 6: Commit** `feat: laterais do notch (Claude e notificações)`.

---

### Task 6: Chave "Avisos do Claude no notch"

**Files:**
- Create: `src/Vitral.App/Claude/ServicoGanchosClaude.cs`
- Modify: `src/Vitral.App/JanelaPrincipal.xaml(.cs)`

**Interfaces:**
- Consumes: Task 2 (`GanchosClaude`).
- Produces:
  - **`ServicoGanchosClaude`:**
    - caminho `%USERPROFILE%\.claude\settings.json`;
    - `bool EstaLigado()`: arquivo ausente ou com defeito → false;
    - `void Ligar(string caminhoExe)` e `void Desligar()`:
      - leem o texto (ou `""` se não existir);
      - aplicam `GanchosClaude`;
      - antes de gravar, copiam o original para `settings.json.vitral-bak` (se existir);
      - gravam em `.tmp` e fazem `File.Move(overwrite: true)`;
      - propagam `FormatException` e `IOException`.
  - **`JanelaPrincipal`:**
    - a `CheckBox x:Name="AvisosClaude"`, com o conteúdo "Avisos do Claude no notch", fica logo abaixo da `IniciarComWindows`, no mesmo estilo;
    - no construtor, `IsChecked = servico.EstaLigado()`;
    - o handler `AvisosClaude_Mudou` liga com `Environment.ProcessPath!` ou desliga;
    - em `FormatException`, mostra "O settings.json do Claude está com defeito; corrija-o antes.";
    - em outra exceção, mostra "Não foi possível alterar os avisos do Claude: {e.Message}";
    - nos dois casos, volta `IsChecked` para `EstaLigado()`.

    É o mesmo padrão do `IniciarComWindows_Mudou`.

- [ ] **Step 1: Implementar.**
- [ ] **Step 2: Build** sem avisos e testes → PASS.
- [ ] **Step 3: Verificação com o Gustavo** (com autorização, porque mexe na configuração do Claude Code):
  - marcar a caixa → o `settings.json` ganha os dois ganchos, e os ganchos dele continuam lá (conferir o arquivo);
  - uma resposta deste Claude terminando acende o notch com "Claude terminou a tarefa";
  - desmarcar → os ganchos saem.
- [ ] **Step 4: Commit** `feat: chave dos avisos do Claude no notch`.

---

### Task 7: Fechamento e instalador 0.7.0

- [ ] **Step 1: Verificação geral com o Gustavo:**
  - uso normal com avisos reais por alguns minutos;
  - Vitral fechado com o Claude rodando: o Claude não é afetado;
  - avisos em rajada;
  - barra do topo desligada.
- [ ] **Step 2:** suítes completas → PASS; build sem avisos.
- [ ] **Step 3: Revisão final** por um agente Opus, com o Review Focus acima, mais:
  - a segurança do canal (ACL, linha grande, conexões em sequência);
  - a escrita no `settings.json`;
  - a tarefa do canal parando no `Sair`;
  - a janela transparente no `Topmost` e no clique que atravessa.

  Corrigir os itens Crítico e Importante com teste primeiro.
- [ ] **Step 4:** com autorização:
  - versão `0.7.0`, build, instalação silenciosa e abertura;
  - commit `chore: versão 0.7.0`;
  - com o ok do Gustavo, juntar na main, enviar ao GitHub e publicar a release `v0.7.0` (Setup e Portable).

  **Atenção:** com a instalação, o caminho do gancho muda para `%LOCALAPPDATA%\VitralApp\current\Vitral.exe`. Desmarcar e marcar a caixa de novo na versão instalada para gravar o caminho certo, ou o `Ligar` já troca o caminho na próxima vez que a caixa for marcada.
