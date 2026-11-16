# Pesquisa: o que os projetos de notch do GitHub fazem e o que serve para o Vitral

Levantamento de 16/11/2026, feito com o `gh` (busca de repositórios, README e leitura do código pela API, sem clonar).
Serve para o Gustavo escolher os próximos passos do notch vivo. Nada daqui foi implementado.

**Licenças:** daqui saem só ideias, técnicas e números de ajuste (durações, raios, parâmetros de mola), que não são
protegidos. Nenhum trecho de código ou imagem foi copiado. Os projetos GPL-3.0 e CC-BY-SA (boring.notch, Atoll para
macOS, DynamicNotch, fly-island, Nectar, DynamicWin) **não podem ter código copiado** para o Vitral. Os de licença
permissiva (MIT, Apache-2.0) até poderiam, com aviso de licença, mas a recomendação é escrever do zero também.

## 1. Os repositórios

Estrelas e data do último push em 16/11/2026.

| Repositório | Plataforma e tecnologia | ★ | Último push | Licença | O que faz bem |
|---|---|---:|---|---|---|
| [TheBoredTeam/boring.notch](https://github.com/TheBoredTeam/boring.notch) | macOS, Swift/SwiftUI | 11 385 | 16/11/2026 | GPL-3.0 | O mais popular. Mídia com cor da capa tingindo o notch, HUD de volume e brilho no lugar do sistema, prateleira de arquivos, espelho da câmera. |
| [Ebullioscopic/Atoll](https://github.com/Ebullioscopic/Atoll) | macOS, Swift (fork do boring.notch) | 5 284 | 16/11/2026 | GPL-3.0 | O mais completo: live activities (timer, gravação, privacidade de câmera e microfone), OSD próprio com vidro, gestos para os quatro lados. |
| [farouqaldori/vibe-notch](https://github.com/farouqaldori/vibe-notch) | macOS, Swift | 2 534 | 20/04/2026 | Apache-2.0 | Sessões do Claude Code no notch: spinner "trabalhando", aprovar e negar no notch, som só se a sessão não estiver em foco. |
| [Lakr233/NotchDrop](https://github.com/Lakr233/NotchDrop) | macOS, Swift | 2 129 | 18/05/2026 | MIT | Bandeja de arquivos arrastados para o notch; hover em dois passos (pré-abertura e aberto). |
| [erha19/ping-island](https://github.com/erha19/ping-island) | macOS, Swift | 1 145 | 15/10/2026 | Apache-2.0 | Doze agentes de código; mascote destacável; pedidos voltam ao terminal quando o usuário está ausente; atalhos globais. |
| [flyl1u/fly-island](https://github.com/flyl1u/fly-island) | **Windows**, Tauri 2 + React + Rust | 816 | 16/11/2026 | GPL-3.0 | O mais próximo do Vitral em ideia: agentes no Windows, luz de 4 estados, anéis de cota, regras de quando avisar e tocar som, documentação de desenho muito boa. |
| [MoFeng2223/BrimDeck](https://github.com/MoFeng2223/BrimDeck) | **Windows, WPF .NET 10** | 766 | 16/11/2026 | Apache-2.0 | A melhor referência técnica para o Vitral: curvas que preservam a velocidade, sombra em cache desligada durante a animação, "ponte" de hover, política de tela cheia por app. |
| [jackson-storm/DynamicNotch](https://github.com/jackson-storm/DynamicNotch) | macOS, Swift | 663 | 13/11/2026 | GPL-3.0 | Física de "esticar e encolher" (squash and stretch), pulso de atenção, fila de prioridade entre atividades. |
| [FlorianButz/DynamicWin](https://github.com/FlorianButz/DynamicWin) | Windows, C# (motor próprio em Skia) | 660 | 15/11/2026 | CC-BY-SA-4.0 | Só publica binários. Vale pelo README: bandeja de arquivos, Bluetooth com bateria. |
| [mrkai77/DynamicNotchKit](https://github.com/mrkai77/DynamicNotchKit) | macOS, biblioteca Swift | 485 | 24/04/2026 | MIT | A origem da forma que todos os de macOS usam (raios 6/14 fechado, 15–19/20–24 aberto); modo compacto com conteúdo dos dois lados do notch. |
| [GEORGEWWWU/NotchPeninsula](https://github.com/GEORGEWWWU/NotchPeninsula) | Windows, Win32 + SkiaSharp | 274 | 16/11/2026 | Apache-2.0 | Acrílico real recortado na forma da ilha (janela auxiliar com Windows.UI.Composition); espectro de áudio leve (Goertzel em 5 bandas); monitor guardado pelo ID de hardware. |
| [kua8/Nectar](https://github.com/kua8/Nectar) | Windows, Tauri + React | 252 | 16/11/2026 | GPL-3.0 | Notch e doca no lugar da barra de tarefas (a mesma ideia do Vitral); molas criticamente amortecidas; modo "Peek" pela borda. |
| [Fuwni/dynamic-island-windows](https://github.com/Fuwni/dynamic-island-windows) ("Atoll para Windows") | **Windows, WPF .NET 10** | 79 | 16/11/2026 | MIT | Mola própria em WPF (passos de 4 ms em `CompositionTarget.Rendering`), conteúdo entrando com desfoque, janela em camadas encolhida quando recolhida para gastar menos. |

Outros vistos e deixados de lado: `samhenrigold/TopNotch` (só esconde o notch), `w0otdev/V-Notch`, `BillyNabil/nabyscape`,
`lb-li/WinIsland` (sem licença), `KrazyManJ/rainmeter-dynamic-island` (skin simples do Rainmeter), `jazzenchen/agent-hud-open`
(HUD de agentes, não notch), dezenas de clones pequenos com menos de 30 estrelas.

## 2. O que o Vitral já tem (para comparar)

Lido em `src/Vitral.App/Barra/JanelaNotch*.cs`, `src/Vitral.Ui/Barra/EstadoNotch*.cs`, `src/Vitral.Ui/Barra/FormaNotch.cs`,
`src/Vitral.Core/Formas/` e `src/Vitral.App/Tema/Tokens.xaml`.

- **Forma:** `Path` refeito a cada quadro com orelhas côncavas de 16 px (fixas) e cantos de baixo que mudam de raio
  (16 normal, 22 aviso, 24 fichas). Formato Ilha (solto da borda, quatro cantos). Corpo em cor sólida com opacidade da
  aparência. **Sem sombra** no corpo.
- **Animação:** largura, altura e raio com `DoubleAnimation` de 320 ms e `BackEase` (amplitude 0,35); o conteúdo troca por
  opacidade em 180 ms. Uma animação nova começa do valor atual, **mas com velocidade zero**.
- **Janela:** fixa no tamanho máximo (492 × sobra + 300 px), `AllowsTransparency`, `WS_EX_NOACTIVATE`, só no monitor
  principal, some em tela cheia.
- **Conteúdo:** relógio, aviso do Claude (terminou / esperando), permissões, perguntas e respostas com fila, lembrete
  com ponto, pendências, agenda com "Entrar", notificações dos apps, atualização do Vitral. Mouse parado 250 ms abre as
  laterais. Conversa já à vista no app não avisa (o mesmo que o fly-island faz com a janela em primeiro plano).
- **Fora do notch:** a mídia é uma ilha separada; volume e brilho estão nos painéis da barra, sem aviso no notch.

**O Vitral já está à frente** de todos os de Windows em: permissões e perguntas do Claude respondidas no notch, agenda com
reunião, notificações dos outros apps com o balão do Windows calado, e o "à vista" que não avisa. Os ganhos estão no
**movimento** e no notch **fechado**, que hoje é só relógio.

## 3. Números que se repetem

| Item | Valores encontrados |
|---|---|
| Raio de cima (orelha) | 6 fechado → 15–19 aberto (DynamicNotchKit e derivados); fixos: 9 (Atoll Win), 13 (Nectar), 14 (NotchPeninsula), 16 (BrimDeck, Vitral) |
| Raio de baixo | 14 fechado → 20–24 aberto (macOS); 12–18 nos de Windows |
| Abrir | mola resposta 0,42 s / amortecimento 0,8 (boring.notch, vibe-notch, ping-island); 0,46 / 0,76 (DynamicNotch); 360 ms (BrimDeck); 450 ms (NotchPeninsula); 560 ms com 0,8% de repique (fly-island) |
| Fechar | **sem repique** em todos: 0,45 / 1,0 (macOS); 0,75 × a duração de abrir (BrimDeck); 380 ms criticamente amortecida (fly-island) |
| Repique | de 0,3% (BrimDeck, "sentido mais que visto") a leve; ninguém usa o repique grande |
| Conteúdo entrando | escala 0,8–0,98 ancorada no topo + opacidade, às vezes desfoque 10–18 → 0, começando 0–120 ms depois da casca |
| Hover para abrir | 0 (BrimDeck, NotchDrop), 100 ms (Atoll Win), 240 ms (ping-island), 300 ms (boring.notch, fly-island); o Vitral usa 250 ms |
| Fechar ao sair | 100 ms (macOS) a 400 ms (fly-island) |
| Sombra | só aberto: preta 0,5–0,7, raio 6–20 |

## 4. Ideias para o Vitral, em ordem de valor

Esforço: **P** = até meio dia, **M** = 1 a 2 dias, **G** = 3 dias ou mais. Risco é o de quebrar algo ou não funcionar
bem no Windows.

### Visual e animação

| # | Ideia | De onde veio | Esforço | Risco |
|---|---|---|---|---|
| V1 | **Mola de verdade, que guarda a velocidade.** Trocar as três `DoubleAnimation` com `BackEase` por uma mola calculada a cada quadro (`CompositionTarget.Rendering`, passos de até 4 ms) com valor, velocidade e alvo. Quando um aviso chega no meio da abertura de uma ficha, a forma muda de rumo sem tranco (hoje recomeça parada). Abrir com repique leve (amortecimento ~0,8), fechar sem repique (~1,0). Desligar o laço quando parada e respeitar o `Movimento.Ligado`. | BrimDeck (curva Hermite que preserva velocidade, mola com passo de 1/240 s), Atoll para Windows (mola por sub-passos), os de macOS (abrir 0,42/0,8, fechar 0,45/1,0) | M | Baixo: troca contida em `Animar` e `Redesenhar`; testável sem tela |
| V2 | **Orelhas que crescem junto.** Animar também o raio de cima: menor com o notch fechado, maior nas fichas (proporção dos de macOS: 6 → 19 com o de baixo 14 → 24). Hoje a orelha é fixa em 16 e as fichas grandes parecem "presas" por uma emenda fina. Manter a escolha do perfil como base e escalar. | DynamicNotchKit, boring.notch, vibe-notch | P | Baixo: `FormaNotch.Caminho` já recebe a orelha |
| V3 | **Conteúdo que "nasce" do notch.** O painel novo entra com escala 0,94 → 1 ancorada no topo e opacidade, 60–120 ms depois da casca começar a crescer; o que sai some em 120–150 ms, mais rápido que o que entra. Desfoque só se o desempenho deixar (ver V5). | DynamicNotchKit, Atoll para Windows, fly-island | P | Baixo |
| V4 | **Sombra só quando aberto.** Sombra preta suave (opacidade ~0,4, raio ~14) nas fichas e no aviso, para o notch "flutuar" sobre o conteúdo das janelas; nenhuma no relógio. Guardar a sombra em `BitmapCache` e desligá-la enquanto a forma anima. | BrimDeck (cache desligado durante o movimento, ligado de novo 250 ms parado), boring.notch | P–M | Médio: sombra em janela em camadas custa CPU; medir |
| V5 | **Janela em camadas do tamanho certo.** Com `AllowsTransparency`, o Windows copia a janela inteira a cada quadro. Hoje ela tem sempre o tamanho da maior ficha (492 × ~330); encolher para a forma atual + margem quando recolhido, e crescer **antes** de abrir. Deixa as animações mais lisas e libera o desfoque do V3. | Atoll para Windows (janela recolhida = pílula + 150 px) | M | Médio: posição e escala (DPI) já deram trabalho no Vitral (0.9.5) |
| V6 | **Pulso de atenção.** Quando chega algo novo com o notch já aberto (mais um pedido na fila, a mesma sessão avisando de novo), a forma dá um pulso curto (+20 de largura, +6 de altura, volta em ~0,15 s) em vez de só trocar o "+N". | DynamicNotch (+30/+10), vibe-notch (bounce) | P | Baixo (depende do V1 para ficar bonito) |
| V7 | **Área de hover que chega ao topo da tela.** Jogar o mouse para o alto (borda de cima, y = 0) deve contar como "em cima do notch", com uma faixa estendida invisível acima e dos lados, e uma "ponte" entre a borda e o corpo para o cursor não escapar no caminho. | boring.notch (área extra de 30 pt), BrimDeck (ponte de hover), Atoll para macOS (faixa do pixel do topo) | P | Baixo |

### Recursos novos

| # | Ideia | De onde veio | Esforço | Risco |
|---|---|---|---|---|
| R1 | **O Claude trabalhando, com o notch fechado.** Atividade compacta dos dois lados do relógio: à esquerda o símbolo do Claude girando ou um spinner de glifos (`· ✢ ✳ ✻`) na cor do Claude enquanto alguma sessão roda; à direita o número de sessões rodando. Esperando você = âmbar; terminou = check que some sozinho. O relógio continua no meio. Precisa de um sinal "começou / parou" pelos ganchos (`UserPromptSubmit` ou `PreToolUse`, e `Stop`). | vibe-notch (spinner e alargar os lados), DynamicNotchKit (compacto com os dois lados), fly-island (luz de 4 estados, contador de sessões) | M | Médio: mais ganchos e mais mensagens pelo canal; o notch fechado fica mais largo (ilhas se reposicionam) |
| R2 | **HUD de volume e brilho no notch.** Ao mudar o volume (teclas, fone) ou o brilho pelo Vitral, o notch desce uma pílula com ícone e barra por ~1,5 s, como o aviso. O sinal do volume já existe (`CoreAudio.cs`). Primeiro passo: só mostrar no notch, sem esconder o flyout do Windows; esconder o do Windows é um passo à parte e arriscado. É o "integrar na barra do topo" pedido em 27/10. | boring.notch e Atoll para macOS (substituem o do sistema); nenhum de Windows substitui o flyout (Atoll Win, BrimDeck, NotchPeninsula só mostram ao lado) | M | Médio: dois HUDs ao mesmo tempo até decidir o do Windows; brilho por DDC/CI ainda falha no monitor do Gustavo |
| R3 | **Mídia viva no notch fechado.** Com algo tocando, capa pequena à esquerda e 4–5 barras de áudio à direita do relógio; o clique ou o hover abre o painel de mídia que já existe. As barras vêm do áudio que sai (WASAPI loopback) em 5 bandas por Goertzel, que é barato; a cor média da capa tinge as barras. Pode substituir a ilha de mídia separada. | boring.notch (capa + visualizador + cor da capa), NotchPeninsula (Goertzel em 5 bandas), Atoll para Windows (pico WASAPI) | G | Médio: concorre por espaço com o R1 (precisa de regra de prioridade, ver R8); loopback custa um pouco de CPU |
| R4 | **Avisar só o que o Gustavo pediu.** "Claude terminou" e o som só para turnos iniciados por uma mensagem digitada (no registro da sessão, `turnOrigin: human`); retornos de tarefas em segundo plano, de subagentes e continuações automáticas não descem. Esperar o turno ficar parado ~6 s antes de avisar e avisar uma vez por mensagem. | fly-island (regra de "conclusão" e de som) | P–M | Baixo: só filtra; conferir o campo na versão do Claude Code instalada |
| R5 | **Ausente, o pedido volta para o app.** Se não há uso de teclado e mouse há N minutos (`GetLastInputInfo`), a permissão não espera os 30 s no notch: sai na hora para o app do Claude, que guarda até a volta. Evita "pedido sem resposta" quando o Gustavo saiu da mesa. | ping-island (proteção por ociosidade) | P | Baixo |
| R6 | **Atalhos de teclado nas fichas.** Enquanto uma ficha está na tela, `Ctrl+Alt+Y` permite, `Ctrl+Alt+N` nega, `Ctrl+Alt+1..4` escolhe a opção da pergunta. Atalhos globais (`RegisterHotKey`) só durante a ficha, porque a janela não pega foco. O Vitral já ignora cliques nos primeiros 0,5 s, como o fly-island planeja. | fly-island (plano de 06/11), ping-island (atalhos globais) | P–M | Médio: conflito com atalhos de outros apps; escolher combinações raras |
| R7 | **Lembrar de novo uma pergunta esquecida.** Uma pergunta do Claude (que fica até 5 min) que segue sem resposta depois de 1–2 min dá um pulso (V6) e, com o som ligado, toca uma vez. Uma vez por espera. | fly-island (B6.4.1, "lembrar se não respondeu") | P | Baixo |
| R8 | **Fila de prioridade das atividades do notch fechado.** Quando houver mais de uma (Claude trabalhando, mídia, reunião em breve, HUD), uma ordem fixa decide quem aparece; dar ao Gustavo a ordem nas Configurações. | DynamicNotch (fila configurável), fly-island (prioridade arrastável) | M | Baixo, mas só vale depois do R1 ou do R3 |
| R9 | **Cota do Claude na lateral.** Dois anéis pequenos (5 h por fora, 7 dias por dentro) mostrando quanto **resta**, na lateral esquerda ao passar o mouse. | fly-island, BrimDeck | M | Alto: a fonte dos números não é oficial e pode mudar |
| R10 | **Bandeja de arquivos.** Arrastar um arquivo até o notch o guarda ali por um tempo; arrastar de volta solta em outro lugar. | NotchDrop, boring.notch, DynamicWin | G | Médio; valor duvidoso para o uso do Gustavo |
| R11 | **Notch no monitor certo.** Escolher o monitor do notch e guardá-lo pelo ID de hardware (o índice muda quando o monitor reconecta), ou seguir o monitor do mouse depois de ~1 s parado lá. | NotchPeninsula (ID de hardware), Atoll para Windows (segue o mouse após 1,2 s), fly-island (principal, mouse ou janela da frente) | G | Alto: mexe na reserva da barra e nas ilhas de todos os monitores |

**Fora daqui:** vidro de verdade no notch (acrílico recortado na forma por uma janela auxiliar com
`Windows.UI.Composition`, como no NotchPeninsula) é assunto da `docs/pesquisa-visual-premium.md`. O notch preto chapado
é o padrão de todos os projetos grandes; os que oferecem vidro deixam como opção.

## 5. As cinco mais fortes

1. **V1, mola que guarda a velocidade:** muda a sensação de tudo o que o notch já faz, com troca pequena e contida.
2. **R1, o Claude trabalhando com o notch fechado:** dá uso ao notch quando ele é só relógio, no assunto que o Gustavo
   mais usa.
3. **R2, HUD de volume e brilho no notch:** pedido antigo de integrar os painéis do Windows na barra do topo, com o sinal
   do volume já pronto.
4. **R4 + R5, avisar só o que importa e devolver o pedido quando ausente:** baratos e cortam ruído e pedidos perdidos.
5. **V2 + V3, orelhas e conteúdo que crescem junto:** o notch passa a "derreter" para a ficha em vez de esticar uma caixa.

## 6. Notas técnicas para quando for fazer

- **Mola em WPF:** os dois projetos WPF fazem igual: um objeto com valor, velocidade e alvo, passo semi-implícito
  (`v += (-k·(x-alvo) - c·v)·h; x += v·h`) com sub-passos de no máximo 4 ms e `dt` limitado a 50 ms, rodando em
  `CompositionTarget.Rendering` e se desligando quando para (|x − alvo| e |v| pequenos). Valores do Atoll para Windows:
  rigidez 300–340, amortecimento 26–31. O BrimDeck escreve a mesma mola por frequência (16) e razão de amortecimento
  (0,7), que é mais fácil de ajustar.
- **Tela cheia:** o Vitral já tem a sua regra. Os de Windows combinam `SHQueryUserNotificationState` (3 e 4) com "a
  janela da frente cobre o monitor" (tolerância de 2 px, ignorando `Progman`, `WorkerW`, `Shell_TrayWnd` e janelas
  `DWMWA_CLOAKED`). O BrimDeck ainda tem regra por app (esconder, virar só um traço, ficar).
- **Clique fora:** o NotchPeninsula usa `WM_INPUT` (entrada bruta) para não perder o clique quando o DWM engasga em jogos.
- **Ficar por cima:** todos reafirmam `HWND_TOPMOST` de tempos em tempos (2 s no NotchPeninsula, a cada mudança de
  janela da frente no BrimDeck via `SetWinEventHook`).
