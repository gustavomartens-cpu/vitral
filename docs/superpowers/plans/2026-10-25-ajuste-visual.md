# Ajuste visual — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** logo novo (V de vidro) e um sistema de movimento leve aplicado à doca, à barra do topo, aos menus e à janela principal.

**Architecture:** as contas puras (escala da lupa, posição do indicador, atraso da cascata, duração com a chave geral) ficam em `Vitral.Ui/Tema`, com testes. As peças WPF (*attached properties* em `Vitral.App/Tema/Efeitos.cs`, a chave `Movimento` e os tokens em `Tokens.xaml`) usam essas contas e são ligadas no XAML de cada superfície.

**Tech Stack:** .NET 10, WPF (Storyboard/DoubleAnimation, BackEase/CubicEase), CommunityToolkit.Mvvm, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-25-ajuste-visual-design.md`

## Global Constraints

- Sem biblioteca nova: só animações nativas do WPF.
- As peças mexem só em `RenderTransform` e `Opacity`, nunca no layout (spec §3.3).
- Opção "Efeitos de animação" do Windows desligada → estado final direto, duração zero (spec §3.2).
- Tokens: `DuracaoRapida` 120 ms, `DuracaoMedia` 180 ms, `DuracaoLenta` 260 ms, `DuracaoClique` 60 ms, `DuracaoBrilho` 550 ms; `CurvaMola` = `BackEase` EaseOut Amplitude 0,35; `CurvaSuave` = `CubicEase` EaseOut.
- Âmbar continua o destaque do tema; nada de trocar cores fora do logo e dos menus.
- Textos e comentários em português, no estilo do código vizinho.
- Build com `TreatWarningsAsErrors`; parar o processo `Vitral` antes de compilar o App.
- Comando de teste: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet test --nologo -v q'`.

## Review Focus

1. **Animações do Windows desligadas** → nada se mexe e nada fica no meio do caminho (escala 1, opacidade 1, indicador na posição). Teste `Duracao_zero_com_animacao_desligada` (Task 1) e conferência na tela (Task 7).
2. **Mouse sai da doca no meio da lupa ou do clique** → todos os ícones voltam a 1; nenhum fica grande ou afundado. Conferência na tela (Task 4), com o mouse saindo rápido por cima e por baixo.
3. **Seletor que aparece depois** (aba trocada, cartão desabilitado, janela restaurada de minimizada) → o indicador vai para a opção certa na primeira exibição, sem deslizar do zero e sem largura zero. Teste `Alvo_relativo_ao_painel_deslocado` (Task 1) e conferência (Task 6).
4. **Menu aberto de novo** → cada abertura assenta do começo (não abre já parado nem pela metade). Conferência (Task 5).
5. **Escala de 125%/150% ou monitor trocado** → a lupa mede em DIPs e o ícone não sai da doca. Conferência (Task 4, com a escala que o Gustavo usa).

---

### Task 1: Contas do movimento

**Files:**
- Create: `src/Vitral.Ui/Tema/Movimento.cs` (classes estáticas `Lupa`, `Deslize`, `Cascata`, `TempoMovimento`)
- Test: `tests/Vitral.Ui.Tests/MovimentoTests.cs`

**Interfaces:**
- Produces:
  - `Lupa.Escala(double distancia) → double` = `1 + 0.22 * Math.Max(0, 1 - Math.Abs(distancia) / 90)`; constantes `Lupa.Maxima = 1.22`, `Lupa.Alcance = 90`.
  - `Deslize.Alvo(Retangulo item, Retangulo painel) → (double X, double Largura)` = `(item.X - painel.X, item.Largura)` (usa `Vitral.Ui.Barra.Retangulo`).
  - `Cascata.Atraso(int indice) → TimeSpan` = `min(indice × 40 ms, 200 ms)`; índice negativo → zero.
  - `TempoMovimento.Duracao(TimeSpan normal, bool ligado) → TimeSpan` = `ligado ? normal : TimeSpan.Zero`.

- [ ] **Step 1: Testes que falham** (`MovimentoTests`):
  - `Lupa_maxima_sob_o_cursor`: `Escala(0) == 1.22`.
  - `Lupa_meio_alcance`: `Escala(45)` ≈ 1.11 (precisão 3).
  - `Lupa_fora_do_alcance`: `Escala(90) == 1` e `Escala(300) == 1`.
  - `Lupa_simetrica`: `Escala(-30) == Escala(30)`.
  - `Alvo_relativo_ao_painel_deslocado`: item (130, 5, 60, 24), painel (100, 0, 300, 34) → (30, 60).
  - `Cascata_em_passos_de_40_ate_200`: índices 0,1,2,5,9 → 0, 40, 80, 200, 200 ms; `Atraso(-1) == 0`.
  - `Duracao_zero_com_animacao_desligada`: `Duracao(180ms, false) == Zero`; `Duracao(180ms, true) == 180ms`.
- [ ] **Step 2: Rodar** `dotnet test tests/Vitral.Ui.Tests` → FAIL (tipos não existem).
- [ ] **Step 3: Implementar** em `src/Vitral.Ui/Tema/Movimento.cs`, namespace `Vitral.Ui.Tema`.
- [ ] **Step 4: Rodar** → PASS.
- [ ] **Step 5: Commit** `feat(tema): contas do movimento`.

---

### Task 2: Logo V de vidro e ícone

**Files:**
- Modify: `src/Vitral.App/Tema/Logo.xaml`
- Create: `tools/gerar-icone.ps1`
- Modify (gerado): `src/Vitral.App/Recursos/vitral.ico`

**Interfaces:**
- Produces: recurso `Logo` (mesmo nome, `DrawingImage`, viewBox 0..100) com os três cacos da spec §5; recurso novo `LogoPequeno` (mesmo desenho, contorno 4) para 16–24 px.

- [ ] **Step 1: `Logo.xaml`**: trocar o `DrawingGroup` pelos três `GeometryDrawing` (polígonos, `Pen` `#0F1014` espessura 3, `LineJoin=Round`), com o retângulo transparente 0,0,100,100 para manter a caixa. Meio com `LinearGradientBrush` 0,0→1,1 `#FFD27A`→`#D98E1C`. Acrescentar `LogoPequeno` com `Pen` de espessura 4.
- [ ] **Step 2: `tools/gerar-icone.ps1`**: script PowerShell (`pwsh`) que carrega `PresentationCore`/`PresentationFramework`, lê `Logo.xaml` com `XamlReader`, desenha `LogoPequeno` em 16 e 24 px e `Logo` em 32, 48, 64 e 256 px (`DrawingVisual` + `RenderTargetBitmap` 96 dpi, fundo transparente), codifica cada um em PNG e grava o ICO (cabeçalho `ICONDIR` + 6 `ICONDIRENTRY` + PNGs; largura/altura 0 para 256). Roda numa thread STA (`powershell -STA` ou `[System.Threading.Thread]` STA).
- [ ] **Step 3: Rodar** `pwsh -NoProfile -STA -File tools/gerar-icone.ps1` → Expected: imprime os 6 tamanhos e o caminho do `vitral.ico`; o arquivo muda de tamanho.
- [ ] **Step 4: Build** do App sem avisos; abrir a build de desenvolvimento.
- [ ] **Step 5: Verificação com o Gustavo:** logo novo no botão Iniciar da doca e no cabeçalho da janela; ícone novo na bandeja (16 px) e na janela.
- [ ] **Step 6: Commit** `feat: logo V de vidro`.

---

### Task 3: Tokens, chave geral, peças e barra do topo

**Files:**
- Modify: `src/Vitral.App/Tema/Tokens.xaml`
- Create: `src/Vitral.App/Tema/Movimento.cs` (classe `Movimento` do App: `bool Ligado`, evento `Mudou`), `src/Vitral.App/Tema/Efeitos.cs`
- Modify: `src/Vitral.App/Barra/IlhaStatus.xaml`, `IlhaRelogio.xaml`, `IlhaMidia.xaml` (itens), `src/Vitral.App/Barra/IlhasConteudo.xaml` (`BotaoIlha`), `src/Vitral.App/Barra/JanelaPainel.cs`

**Interfaces:**
- Consumes: Task 1 (`TempoMovimento.Duracao`, `Cascata.Atraso`).
- Produces (`Vitral.App.Tema.Efeitos`, xmlns `tema` = `clr-namespace:Vitral.App.Tema`):
  - `Efeitos.Afundar` (bool): escala 0,9 em `DuracaoClique` no `PreviewMouseLeftButtonDown`; volta a 1 em `DuracaoRapida` com `CurvaMola` no `PreviewMouseLeftButtonUp`, `MouseLeave` e `LostMouseCapture`. Usa um `ScaleTransform` próprio dentro de um `TransformGroup` no `RenderTransform` (cria o grupo se não existir; reaproveita se existir), `RenderTransformOrigin` 0.5,0.5 se não definido.
  - `Efeitos.Assentar(FrameworkElement alvo, AssentarDe de, bool cascata)` — método público estático (não *attached*), chamado por quem mostra a janela/popup: deslocamento inicial −8 (`Cima`) ou +8 (`Baixo`), opacidade 0 → posição em `DuracaoLenta` com `CurvaMola`, opacidade em `DuracaoMedia` com `CurvaSuave`. Com `cascata`, cada filho direto do `Panel` raiz do conteúdo: 4 px + fade, `BeginTime = Cascata.Atraso(i)`. Sempre recomeça do estado inicial.
  - `enum AssentarDe { Cima, Baixo }`.
  - `Efeitos.Brilho` (bool) e `Efeitos.Perigo` (bool) — só declarados aqui; usados nas Tasks 5 e 6.
  - Todas as durações passam por `TempoMovimento.Duracao(d, Movimento.Ligado)`.

- [ ] **Step 1: Tokens** em `Tokens.xaml`: `Duration`/`TimeSpan` `DuracaoRapida`, `DuracaoMedia`, `DuracaoLenta`, `DuracaoClique`, `DuracaoBrilho`; `BackEase x:Key="CurvaMola" EasingMode="EaseOut" Amplitude="0.35"`, `CubicEase x:Key="CurvaSuave" EasingMode="EaseOut"`. Em `Efeitos.cs`, os valores vêm de `Application.Current.Resources` com o mesmo valor como padrão (para os testes de fumaça e se o recurso faltar).
- [ ] **Step 2: `Movimento`** (App): `Ligado => SystemParameters.ClientAreaAnimation`; assina `SystemEvents.UserPreferenceChanged` (categoria `General`/`VisualStyle`) e dispara `Mudou`.
- [ ] **Step 3: `Efeitos.cs`** com `Afundar` e `Assentar` como acima.
- [ ] **Step 4: Topo:** no estilo `BotaoIlha` e nos itens clicáveis das ilhas (CPU/MEM, setinha, rede, volume, bateria, relógio, mídia), fundo `SuperficieHover` no hover (onde ainda não há) e `tema:Efeitos.Afundar="True"`. Na `JanelaPainel.Mostrar`, trocar a animação de 8 px atual por `Efeitos.Assentar(_casca, AssentarDe.Cima, cascata: true)`; a cascata usa o `Panel` raiz do conteúdo de cada painel (os `UserControl` dos painéis têm um `StackPanel` raiz; onde não tiver, só o assentar).
- [ ] **Step 5: Build** sem avisos e testes → PASS.
- [ ] **Step 6: Verificação com o Gustavo:** hover e clique nos itens das ilhas; cada painel (calendário, Wi-Fi, volume, bateria, mídia, ícones) desce assentando com o conteúdo em cascata; com "Efeitos de animação" desligado no Windows, painel aparece parado.
- [ ] **Step 7: Commit** `feat(tema): movimento e barra do topo`.

---

### Task 4: Doca — lupa, clique, marca e miniaturas

**Files:**
- Modify: `src/Vitral.App/Tema/Efeitos.cs` (peça `Lupa`), `src/Vitral.App/Doca/ConteudoDoca.xaml`, `src/Vitral.App/Doca/JanelaMiniaturas.cs`

**Interfaces:**
- Consumes: Task 1 (`Lupa.Escala`), Task 3 (`Afundar`, `Assentar`).
- Produces: `Efeitos.Lupa` (bool, num `Panel`/`ItemsControl`): no `MouseMove` do elemento, para cada descendente marcado com `Efeitos.AlvoDaLupa="True"`, escala = `Lupa.Escala(xCursor − centroX)` medida em DIPs relativos ao elemento com a lupa, aplicada direto num `ScaleTransform` próprio (no mesmo `TransformGroup` do `Afundar`), origem 0.5,1. No `MouseLeave`, volta todos a 1 em `DuracaoRapida` com `CurvaSuave`. Com `Movimento.Ligado == false`, a lupa não faz nada.

- [ ] **Step 1: Peça `Lupa`** em `Efeitos.cs`.
- [ ] **Step 2: Doca**: no `StackPanel` raiz da `ConteudoDoca`, `tema:Efeitos.Lupa="True"`; no `Grid` de 30 px de cada ícone de app, na imagem do logo e no glifo da pesquisa, `tema:Efeitos.AlvoDaLupa="True"` e `tema:Efeitos.Afundar="True"` no botão. Marca do app ativo: trocar os `Setter` de `Width`/`Background` do `DataTrigger Ativo` por `EnterActions`/`ExitActions` com `DoubleAnimation` de `Width` 6↔16 em `DuracaoMedia` (`CurvaMola`) e `ColorAnimation` do pincel cinza↔âmbar; o brilho (`DropShadowEffect`) continua por `Setter`.
- [ ] **Step 3: Miniaturas**: em `JanelaMiniaturas.Mostrar`, depois do `Show`, `Efeitos.Assentar(_casca, AssentarDe.Baixo, cascata: false)`.
- [ ] **Step 4: Build** sem avisos e testes → PASS.
- [ ] **Step 5: Verificação com o Gustavo:** lupa acompanha o mouse e os vizinhos crescem; sair rápido por cima e por baixo devolve tudo a 1; clique afunda; arrastar um fixado continua funcionando; a marca do ativo cresce com animação ao trocar de app; o pulso de atenção continua; miniaturas sobem assentando; escala do monitor do Gustavo.
- [ ] **Step 6: Commit** `feat(doca): lupa e movimento`.

---

### Task 5: Menus com a cara do Vitral

**Files:**
- Modify: `src/Vitral.Ui/MenuBandeja.cs` (`ItemMenu` da bandeja: `string? Icone = null`), `src/Vitral.Ui/Doca/GrupoApps.cs` (`ItemMenu` da doca: `string? Icone = null, bool Perigo = false`), `src/Vitral.Ui/Doca/DocaViewModel.cs` (`Menu` com ícones e perigo)
- Modify: `src/Vitral.App/Tema/Estilos.xaml` (estilos implícitos de `ContextMenu`, `MenuItem`, `Separator`), `src/Vitral.App/Doca/MenuDoca.cs`, `src/Vitral.App/Bandeja.cs`
- Test: `tests/Vitral.Ui.Tests/DocaTests.cs` (menu), `tests/Vitral.Ui.Tests/MenuBandejaTests.cs` (existente; acrescentar)

**Interfaces:**
- Produces: glifos (spec §4.3): Fixar/Desafixar ``, Abrir nova janela ``, Fechar janela/Fechar todas `` (perigo), Abrir Vitral ``, Sair ``; perfis sem ícone.

- [ ] **Step 1: Testes que falham:**
  - `Menu_da_doca_tem_icones_e_perigo`: para um fixado aberto com uma janela, ícones `["", "", ""]` e `Perigo` `[false, false, true]`.
  - `Menu_da_bandeja_tem_icones_em_abrir_e_sair`: "Abrir Vitral" → ``, "Sair" → ``, perfis → `null`.
- [ ] **Step 2: Rodar** → FAIL. **Step 3: Implementar** os campos e os valores → PASS.
- [ ] **Step 4: Estilos** em `Estilos.xaml` (spec §4.3): `ContextMenu` (fundo `#DB1E2026`, borda `BordaVidro`, cantos 12, padding 6, `DropShadowEffect` 0/24/0,45, `HasDropShadow=False`); `MenuItem` (altura 32, cantos 8, coluna de ícone 16 na `FonteIcones` lendo `Icon` como texto, `InputGestureText` à direita em `TextoSecundario`; hover `#29F2B544` com ícone `Ambar`; com `tema:Efeitos.Perigo="True"`: hover `#2EE85454`, texto/ícone `#FF8A8A`, ícone com `TranslateTransform` X 0→3 em `DuracaoRapida`; `IsChecked` mostra ✓ na coluna do ícone); `Separator` (1 px `#17FFFFFF`, margem 6,5).
- [ ] **Step 5: Abertura**: no `Opened` de cada `ContextMenu`, `Efeitos.Assentar(menu, de, cascata: false)`. O menu da doca sobe dela (`AssentarDe.Baixo`). O menu da bandeja abre junto do ícone do Vitral, que fica no painel de ícones do topo com a doca ligada e na barra do Windows, embaixo, com ela desligada. Por isso o lado vem da posição do cursor: na metade de cima do monitor, `AssentarDe.Cima`; na de baixo, `AssentarDe.Baixo`.
- [ ] **Step 6: Usar os campos**: `MenuDoca` põe `Icon = item.Icone` (texto) e `Efeitos.SetPerigo(mi, item.Perigo)`; `Bandeja.MontarMenu` põe `Icon = item.Icone`. Se o estilo implícito não chegar ao menu do H.NotifyIcon, aplicar `Style = (Style)Application.Current.FindResource(typeof(ContextMenu))` na criação (spec §8).
- [ ] **Step 7: Build** sem avisos e testes → PASS.
- [ ] **Step 8: Verificação com o Gustavo:** menu da doca e da bandeja com vidro escuro, ícones, pílula âmbar, perigo vermelho com o ícone andando; cada abertura assenta do começo; ✓ no perfil ativo.
- [ ] **Step 9: Commit** `feat(tema): menus com a cara do Vitral`.

---

### Task 6: Janela principal — deslize, cartões, botões e brilho

**Files:**
- Modify: `src/Vitral.App/Tema/Efeitos.cs` (peças `Deslize` e `Brilho`), `src/Vitral.App/Tema/Estilos.xaml` (`AbasVitral`, `AbaVitral`, `SeletorPilula`, `CartaoVidro`, `BotaoBase`, `BotaoPrimario`)

**Interfaces:**
- Consumes: Task 1 (`Deslize.Alvo`), Task 3 (`Afundar`).
- Produces:
  - `Efeitos.Deslize` (bool, num `Selector`): o template do seletor tem um `Canvas` de fundo com um elemento `x:Name="Indicador"` (procurado com `Template.FindName`); a cada `SelectionChanged`, `SizeChanged` e `Loaded`, calcula `Deslize.Alvo` entre o container selecionado e o painel de itens e anima `Canvas.Left` e `Width` do indicador em `DuracaoMedia` com `CurvaMola`; na primeira vez (indicador sem posição ou largura 0) vai direto. Sem seleção → indicador escondido.
  - `Efeitos.Brilho` (bool, num `Button`): o template tem um `Rectangle x:Name="Faixa"` com gradiente branco (transparente → 55% → transparente), `SkewTransform` −20 e `TranslateTransform`; no `MouseEnter`, `X` de −largura×0,6 a largura×1,2 em `DuracaoBrilho`; recortado pelo `Border` com `ClipToBounds`.
- [ ] **Step 1: `Deslize` e `Brilho`** em `Efeitos.cs`.
- [ ] **Step 2: Abas**: no template de `AbasVitral`, o painel de cabeçalhos ganha o `Canvas` com o `Indicador` (2 px, `Ambar`, cantos 1, brilho âmbar) por baixo; `tema:Efeitos.Deslize="True"` no `TabControl`; `AbaVitral` perde o `Sublinhado` próprio (mantém a cor âmbar do cabeçalho selecionado).
- [ ] **Step 3: Pílulas**: o template do `SeletorPilula` ganha um `Grid` com o `Canvas`+`Indicador` (pílula `Ambar`, `RaioPilula`) por baixo do `ItemsPresenter`; o item selecionado deixa de pintar o próprio fundo (só troca o texto para `TextoSobreAmbar`); hover continua `SuperficieHover` nos não selecionados.
- [ ] **Step 4: Cartões, botões, brilho**: `CartaoVidro` ganha `RenderTransform` `TranslateTransform` e gatilhos de `IsMouseOver` com `EnterActions`/`ExitActions` (Y 0↔−2 e cor da borda → âmbar 35% em `DuracaoRapida`); `BotaoBase` com `tema:Efeitos.Afundar="True"` (setter de attached property); `BotaoPrimario` com a `Faixa` no template e `tema:Efeitos.Brilho="True"`.
- [ ] **Step 5: Build** sem avisos e testes → PASS.
- [ ] **Step 6: Verificação com o Gustavo:** abas e pílulas deslizam com mola; indicador certo ao trocar de perfil, de aba, com o cartão desabilitado e depois de restaurar a janela minimizada; cartão sobe e esquenta; botões afundam; brilho passa no Aplicar; teclado (setas nas pílulas, Tab) continua funcionando.
- [ ] **Step 7: Commit** `feat(tema): janela principal com movimento`.

---

### Task 7: Fechamento e instalador 0.6.0

- [ ] **Step 1: Verificação geral com o Gustavo:** "Efeitos de animação" do Windows desligado (nada se mexe, nada fica no meio); religado (volta a animar sem reiniciar); uso normal por alguns minutos sem travadas na doca.
- [ ] **Step 2:** suítes completas → PASS; build sem avisos.
- [ ] **Step 3: Revisão final** por um agente Opus, com o Review Focus acima, e o foco extra em: vazamento de handlers (`MouseMove` da lupa, `UserPreferenceChanged`, `Opened` dos menus), storyboards que deixam o valor travado (`FillBehavior`), conflito entre o `TransformGroup` da lupa/afundar e o pulso/drag da doca. Corrigir Crítico e Importante com teste primeiro.
- [ ] **Step 4:** com autorização: versão `0.6.0`, build, instalação silenciosa e abertura pelo `explorer.exe`; commit `chore: versão 0.6.0`; com o ok do Gustavo, juntar na main e enviar ao GitHub.
