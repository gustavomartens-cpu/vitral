# Ajuste visual: logo novo e sistema de movimento

Data: 25/10/2026. Logo novo e efeitos de movimento leves na doca, na barra do topo, nos menus e na janela principal.
As ideias vêm do Watermelon UI e são refeitas com as animações nativas do WPF, sem biblioteca nova.

## 1. Objetivo

O Gustavo quer que o Vitral pareça um app de personalização, vivo e coeso. Hoje há três problemas:

- o logo (quatro quadrados girados, âmbar e azul) diz "Windows mudado", mas não diz "personalização";
- quase nada se mexe: há só o deslize do painel do topo e o pulso de atenção da doca;
- o menu do botão direito é o padrão do tema Fluent, sem a cara do Vitral.

Cada escolha abaixo foi feita por ele numa página de comparação, com exemplos que funcionam. As páginas ficam em
`.superpowers/brainstorm/3934-1792969374/content/`.

## 2. Escolhas do Gustavo

| Item | Escolha | Página |
|------|---------|--------|
| Forma do logo | **V de vidro**: a letra V em três cacos (braço esquerdo, braço direito, triângulo do meio) | `logo-ideias.html`, ideia 4 |
| Cor do logo | **Prata com faísca âmbar**: esquerdo branco, direito cinza claro, meio âmbar | `logo-cor.html`, D |
| Doca | **Lupa**: o ícone sob o mouse cresce e os vizinhos crescem conforme a distância, dentro da altura da doca | `doca-efeitos.html`, A |
| Menus | **Pílula âmbar**: vidro escuro, ícone à esquerda, pílula âmbar no hover, "perigo" em vermelho com o ícone deslizando | `menus.html`, A |
| Topo | **Desce e assenta**: pílula clara no hover dos itens; o painel desce com mola e o conteúdo entra em cascata | `ilhas-paineis.html`, A |
| Janela | **Tudo desliza**: aba e pílula deslizam até a escolha; o cartão sobe; um brilho passa no "Aplicar" | `janela.html`, A |

## 3. Arquitetura: um sistema de movimento no tema

O sistema mora em `src/Vitral.App/Tema/`. As contas que dá para testar (escala, posição, atraso) ficam em
`src/Vitral.Ui/Tema/`, como funções puras, sem dependência do WPF.

### 3.1 Tokens (`Tokens.xaml`)

| Token | Valor | Uso |
|-------|-------|-----|
| `DuracaoRapida` | 120 ms | hover, volta do clique |
| `DuracaoMedia` | 180 ms | troca de seleção, fade de menu |
| `DuracaoLenta` | 260 ms | painel e menu entrando |
| `DuracaoClique` | 60 ms | afundar no clique |
| `DuracaoBrilho` | 550 ms | faixa de brilho do botão principal |
| `CurvaMola` | `BackEase`, `EaseOut`, `Amplitude` 0,35 | passa um pouco do ponto e volta |
| `CurvaSuave` | `CubicEase`, `EaseOut` | só desacelera |

### 3.2 Chave geral (`Movimento`)

`Movimento.Ligado` lê `SystemParameters.ClientAreaAnimation`, que é a opção "Efeitos de animação" do Windows. A
mudança é acompanhada pelo `SystemEvents.UserPreferenceChanged`.

Com a chave desligada, toda peça aplica direto o estado final, com duração zero. A regra mora numa função pura,
`Movimento.Duracao(TimeSpan normal, bool ligado)`, que devolve zero quando a animação está desligada.

### 3.3 Peças

Cada peça é uma *attached property* ligada no XAML com uma linha, por exemplo `tema:Efeitos.Lupa="True"`. Todas
mexem só em `RenderTransform` e `Opacity`, nunca no layout.

1. **Lupa** (`Efeitos.Lupa`, no `ItemsControl` da doca e no painel com o logo e a pesquisa).
   - Enquanto o mouse se move sobre a doca, cada ícone recebe a escala `Lupa.Escala(distância)`. A distância é
     medida em DIPs, do cursor ao centro do botão, só no eixo horizontal.
   - A conta é `1 + 0,22 × max(0, 1 − d / 90)`: no máximo 1,22 sob o cursor e 1 a partir de 90 DIPs.
   - A origem é o centro de baixo do ícone, então ele cresce para cima e cabe nos 44 px do botão.
   - Ao sair da doca, todos voltam a 1 em `DuracaoRapida`, com `CurvaSuave`.
   - No movimento do mouse a escala é aplicada direto, sem animação, para não atrasar em relação ao cursor.
2. **Afundar** (`Efeitos.Afundar`).
   - No `PreviewMouseLeftButtonDown`, a escala vai a 0,9 em `DuracaoClique`. Ao soltar ou sair, volta a 1 em
     `DuracaoRapida`, com `CurvaMola`.
   - Na doca, multiplica a escala da lupa em vez de substituí-la (duas transformações em grupo).
3. **Deslize da seleção** (`Efeitos.Deslize`, num `Selector`: `TabControl` e o `ListBox` do `SeletorPilula`).
   - Um único indicador, parte do template, desliza até o item selecionado. A posição (X e largura) vem de
     `Deslize.Alvo(retanguloDoItem, retanguloDoPainel)`.
   - Anima em `DuracaoMedia`, com `CurvaMola`, a cada `SelectionChanged` e `SizeChanged`. Na primeira exibição, vai
     direto para a posição, sem animar.
   - O sublinhado âmbar das abas (`AbaVitral`) e o fundo âmbar das pílulas passam a ser esse indicador, e não mais um
     por item.
4. **Assentar** (`Efeitos.Assentar`, numa janela ou popup que aparece).
   - Ao mostrar, o conteúdo começa deslocado 8 px e transparente. Ele chega à posição em `DuracaoLenta`, com
     `CurvaMola`, e à opacidade 1 em `DuracaoMedia`.
   - O deslocamento é de −8 px para o que desce do topo e de +8 px para o que sobe da doca, definido por
     `Efeitos.AssentarDe` (`Cima`/`Baixo`).
   - **Cascata** (`Efeitos.Cascata="True"`, só nos painéis do topo): os filhos diretos do conteúdo entram um a um,
     4 px e fade, com o atraso de `Cascata.Atraso(índice) = índice × 40 ms`, limitado a 200 ms.
5. **Brilho** (`Efeitos.Brilho`, no `BotaoPrimario`). Uma faixa clara em gradiente, inclinada 20°, atravessa o botão
   da esquerda para a direita em `DuracaoBrilho` a cada entrada do mouse. Ela fica recortada pela borda do botão.
6. **Hover de cartão** (estilo `CartaoVidro`). O cartão sobe 2 px e a borda vai a âmbar 35%, em `DuracaoRapida`.
   Isso é feito com gatilhos do próprio estilo, sem peça nova.

## 4. Aplicação por superfície

### 4.1 Doca

- **Lupa:** em todos os botões, inclusive o logo e a pesquisa.
- **Afundar:** em todos os botões.
- **Marca do app ativo:**
  - a troca de largura (6 → 16 px) e de cor passa a ser animada, em `DuracaoMedia`;
  - o brilho âmbar aparece junto.
- **Miniaturas:** assentam de baixo (`AssentarDe=Baixo`).
- **Mantido:** o pulso âmbar de atenção não muda.

### 4.2 Barra do topo

- **Itens das ilhas** (CPU e MEM, setinha, rede, volume, bateria, relógio, mídia):
  - pílula clara (`SuperficieHover`) no hover;
  - afundar no clique.
- **Painéis:** a `JanelaPainel` troca o deslize atual de 8 px por Assentar de cima com cascata.

### 4.3 Menus

Um estilo implícito de `ContextMenu`, `MenuItem` e `Separator` no `Estilos.xaml`. Vale para o menu da doca
(`MenuDoca`) e para o da bandeja (`Bandeja`, que já usa o `ContextMenu` do WPF).

- **Fundo:** `#DB1E2026` (86% de opacidade), borda `BordaVidro`, cantos de 12 px, sombra e 6 px de folga interna. O
  fundo não é acrílico, só translúcido (ver §7).
- **Item:**
  - 32 px de altura, cantos de 8 px;
  - coluna de ícone de 16 px, na `FonteIcones`;
  - o texto de atalho aparece à direita, em `TextoSecundario`.
- **Hover:** pílula âmbar 16% (`#29F2B544`), com o ícone em `Ambar`.
- **Perigo:** marcado por `Efeitos.Perigo="True"`.
  - no hover, a pílula fica vermelha 18% e o texto e o ícone vão a `#FF8A8A`;
  - o ícone desliza 3 px para a direita.
- **Abertura:** Assentar, de baixo quando o menu sai da doca e de cima nos demais casos.
- **Ícones:**

  | Item | Glifo |
  |------|-------|
  | Fixar, Desafixar | `E718` |
  | Abrir nova janela | `E710` |
  | Fechar janela, Fechar todas | `E711` |
  | Abrir o Vitral | `E80F` |
  | Sair do Vitral | `E7E8` |
  | Perfis | sem ícone (marcados com ✓) |

  `ItemMenu` ganha os campos opcionais `string? Icone` e `bool Perigo`. "Fechar janela" e "Fechar todas" são perigo.

### 4.4 Janela principal

- **Abas** (`AbaVitral`): deslize do sublinhado âmbar, que mantém o brilho de hoje.
- **Pílulas** (`SeletorPilula`): deslize do fundo âmbar, com o texto do item escolhido em `TextoSobreAmbar`.
- **Cartões:** hover, conforme §3.3, item 6.
- **Botões** (`BotaoBase` e derivados): afundar.
- **`BotaoPrimario`:** brilho.

## 5. Logo

- **Desenho** (`Logo.xaml`, recurso `Logo`, viewBox 100×100). São três polígonos com contorno `#0F1014` de 3 de
  espessura e cantos arredondados:

  | Caco | Pontos | Preenchimento |
  |------|--------|---------------|
  | Esquerdo | `8,12 34,12 50,52 50,92` | `#F2F2F2` |
  | Direito | `92,12 66,12 50,52 50,92` | `#A9ADB6` |
  | Meio | `34,12 66,12 50,52` | gradiente diagonal `#FFD27A` → `#D98E1C` |

- **Ícone do app:**
  - o script `tools/gerar-icone.ps1` desenha o mesmo recurso com WPF (`RenderTargetBitmap`) em 16, 24, 32, 48, 64 e
    256 px, monta o `src/Vitral.App/Recursos/vitral.ico` (PNG dentro do ICO) e grava o arquivo;
  - o ícone gerado entra no repositório, e o script roda à mão quando o desenho mudar;
  - em 16 e 24 px, o contorno fica com 4 de espessura para não sumir.
- **Onde aparece:** o botão Iniciar da doca, o cabeçalho da janela principal, o ícone do app, o atalho do Iniciar e a
  bandeja.

## 6. Testes

**Funções puras em `Vitral.Ui`, com teste escrito antes:**

- `Lupa.Escala`:
  - 1,22 em 0;
  - 1,11 em 45;
  - 1 em 90 e acima;
  - simétrica para os dois lados.
- `Deslize.Alvo`: devolve o X relativo ao painel e a largura do item, inclusive com o painel deslocado.
- `Cascata.Atraso`: 0, 40, 80 ms e assim por diante, até o teto de 200 ms.
- `Movimento.Duracao`: devolve zero com a animação desligada e o valor normal com ela ligada.
- `ItemMenu`: o menu da doca põe os ícones e marca o perigo em "Fechar janela" e "Fechar todas".

**Na tela, com o Gustavo:** cada superfície ao fim da sua etapa, mais a opção "Efeitos de animação" do Windows
desligada (tudo aparece sem movimento) e o ícone novo em 16 px na bandeja.

## 7. Fora desta fase

- Acrílico de verdade nos menus: o popup do WPF precisa de janela própria com composição.
- O notch vivo (notificações e Claude) e os cantos curvos ligando o notch à borda.
- Trocar a cor de destaque do tema (o âmbar continua).

## 8. Riscos

- **Lupa:** roda a cada movimento do mouse sobre a doca. Mexe só em `ScaleTransform` (sem layout) e tem no máximo
  algumas dezenas de botões, então o custo é baixo.
- **Ícone gerado:** o render de 16 px pode borrar. Por isso o contorno é mais grosso nos tamanhos pequenos, e o
  Gustavo confere.
- **Indicador único nas pílulas e abas:** muda o template do `SeletorPilula` e da `AbaVitral`, que todo o editor
  usa. As escolhas continuam pelo `SelectedValue`, então nada muda nos ViewModels.
- **Menu da bandeja:** o H.NotifyIcon abre o `ContextMenu` como popup do WPF. Se o estilo implícito não chegar até
  ele, o estilo passa a ser aplicado direto, com `Style = (Style)FindResource(...)`.
