# Ilha sem fundo (19/11/2026)

## Por quê

Pedido do Gustavo: "a ilha, no canto superior direito, deixasse de existir, mas as informações continuassem ali. Como se
realmente não tivesse um formato de pílula, só as informações em cima da tela mesmo... completamente transparente".

## O que é

Uma terceira escolha em **Formas → Disposição das ilhas**: **"Sem fundo"** (ao lado de "Separadas" e "Barra única"). Por
perfil, com "Não mexer" como as outras formas.

Com ela, a ilha de status (a da direita: CPU/MEM, setinha dos ícones, controles, Wi-Fi, volume, bateria) fica sem vidro,
sem borda e sem cantos, e **sem** a faixa de vidro atrás (que é o que distingue da barra única). As informações ficam no
mesmo lugar de hoje.

## Decisões

- **Só a ilha da direita.** É a única que existe além do notch (o relógio virou o notch). O notch e a doca não mudam.
- **Legibilidade: cor do tema + sombra suave** (escolha do Gustavo, opção 1). Texto e ícones seguem a cor de texto da
  aparência das ilhas e ganham uma sombra suave, escura quando o texto é claro e clara quando o texto é escuro. A sombra só
  existe no modo "Sem fundo". Contraste automático pelo papel de parede ficou de fora.
- **Hover e clique iguais a hoje**: a pílula clarinha de cada item aparece só sob o mouse; os painéis descem como sempre.
- **A faixa do topo continua reservada**: as janelas maximizadas começam abaixo da barra, então atrás das informações há
  sempre o papel de parede. A "Distância" (com respiro / encostadas) continua posicionando a ilha.
- **Prévia da aparência**: no modo "Sem fundo" não desenha a pílula das ilhas; desenha só marquinhas na cor do texto
  representando as informações.

## Abordagens

1. **Reaproveitar o "sem vidro" da barra única, sem a faixa atrás** (escolhida). `VidroIlha` já deixa a ilha
   transparente de verdade (quadro do DWM estendido, acento transparente, borda zerada); falta só não criar a
   `JanelaFundoBarra` e ligar a sombra.
2. Janela nova só para as informações soltas: repetiria a ilha, o hover e a abertura dos painéis. Descartada.
3. Pintar a ilha com fundo de alfa zero pela aparência: o acrílico do Windows não fica transparente de verdade e a borda
   do DWM continua. Descartada.

## Como (para quem constrói)

- `DisposicaoIlhas` ganha `SemFundo` (no fim do enum; o JSON grava texto, então nada antigo muda).
- Regra testável em `Vitral.Ui` (`PosicaoIlhas`): a ilha fica sem vidro na barra única **ou** sem fundo; a faixa atrás
  (`ComFundo`) só na barra única; a sombra só no sem fundo; a cor da sombra pela luminância do texto.
- `ControleBarra.Posicionar` usa essas regras; `JanelaIlha`/`VidroIlha` ganham a sombra (um `DropShadowEffect` na casca,
  refeito quando a aparência muda).
- `Opcoes.DisposicoesIlhas` ganha "Sem fundo"; `PreviaAparencia` desenha o modo novo.
- Fica para o construtor: raio, opacidade e deslocamento exatos da sombra (ponto de partida: sem deslocamento, raio ~6,
  opacidade ~0,6), ajustados no teste na máquina com o Gustavo.

## Fora do escopo

Notch, doca, contraste automático pelo papel de parede.
