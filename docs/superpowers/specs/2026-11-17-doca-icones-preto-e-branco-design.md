# Ícones da doca em preto e branco: desenho curto

Data: 17/11/2026. Pedido do Gustavo; decisões tomadas com ele na conversa da sessão.

## Intenção

Poder deixar os ícones da doca em tons de cinza, por perfil, para uma doca mais sóbria, sem perder as cores
originais: desligar a opção devolve tudo como era. É a primeira etapa de "mudar a cor dos ícones da doca"; a segunda
(ícones próprios do Vitral para os itens do Windows) fica só em proposta, entregue ao fim desta.

## Objetivos

- Opção por perfil **Cor dos ícones: Originais | Preto e branco**, no cartão "Formas da doca" da aba "Barra de
  tarefas e doca", no padrão de Formato, Ícones e Ampliação (pílulas, nulo = "não mexer").
- Em preto e branco, os ícones dos apps (fixados e abertos) aparecem em tons de cinza, com a transparência e as
  bordas lisas preservadas.
- Com o mouse em cima, o ícone volta à cor com um fade curto (~150 ms) e volta ao cinza ao sair.
- Desempenho: a conversão é feita uma vez por ícone e guardada; nada é recalculado a cada quadro nem durante a lupa.

## Não objetivos

- "Um tom" (ícone pintado na cor do texto/destaque): fica para a etapa 2, com os ícones próprios (decisão do Gustavo).
- O V do Iniciar continua colorido (é a marca do Vitral). A lupa já é um glifo na cor do texto: não muda.
- Ícones da bandeja: com a doca ligada eles ficam na setinha da barra do topo, fora da doca.
- O app ativo **não** volta à cor: a marca âmbar embaixo dele já mostra qual é. O pulso âmbar de "pedindo atenção"
  continua por cima do ícone cinza.

## Anti-objetivos

- Bordas serrilhadas ou halo escuro em volta do ícone (perder o alfa).
- Ícone que fica cinza para sempre depois de desligar a opção, ou que precisa reiniciar o Vitral para voltar.
- Doca mais lenta para abrir ou travando na ampliação.

## Abordagens consideradas

- **A. Converter a imagem uma vez e guardar as duas (escolhida).** Cada ícone ganha uma cópia em cinza, calculada
  ponto a ponto mantendo o alfa e guardada junto do original; a doca mostra a cinza por cima da colorida e o hover
  faz o fade. Custo único por ícone, sem ferramenta nova, a conta fica isolada e testável.
- **B. Shader de placa de vídeo.** Pede o compilador de shader do Windows SDK no build e roda a cada quadro
  (inclusive na lupa). Descartada.
- **C. Conversão pronta do WPF para cinza (Gray8) + máscara de opacidade.** O Gray8 perde o alfa; a máscara recompõe
  com duas camadas e costuma serrilhar as bordas. Descartada.

## Como fica (o que foi decidido)

- **Perfil:** `FormasDoca` ganha `CorIcones` (enum `CorIconesDoca { Originais, PretoEBranco }`, nulo = não mexer),
  passando por `ResolverFormas.Mesclar`/`Doca` como Formato, Ícones e Ampliação; `ValoresDoca` leva o valor resolvido
  (padrão Originais). Enum, não bool, para a etapa 2 poder acrescentar valores. O JSON do perfil lê valores
  desconhecidos com tolerância, como os outros enums (`EnumTolerante`).
- **Conta do preto e branco (Core, testável):** função pura sobre pixels BGRA pré-multiplicados (o formato que o WPF
  entrega). A luminância (`0,299·R + 0,587·G + 0,114·B`) passa por uma curva em S (inclinação 12, normalizada para
  0→0 e 255→255) que puxa os escuros para o preto e os claros para o branco; a curva vale sobre a luminância sem o
  alfa e o resultado volta multiplicado por ele (nunca passa do alfa). Alfa intacto. **Mudança de 17/11/2026:** o
  cinza simples ficou "cinza demais"; numa prévia com os ícones reais, o Gustavo escolheu este alto contraste (entre
  alto contraste, branco com traços e branco liso).
- **No App:** um conversor que recebe o `ImageSource` do ícone, rasteriza (o ícone pode ser `BitmapSource` ou não),
  aplica a conta, devolve um `BitmapSource` congelado e guarda o resultado num cache fraco por instância de ícone
  (`ConditionalWeakTable`), para não converter de novo nem segurar ícones de janelas fechadas. Falha na conversão
  devolve o original (nunca some o ícone).
- **Na doca:** a `Image` cinza fica por cima da colorida no mesmo `Grid`; só existe/aparece com a opção ligada
  (recurso dinâmico vindo do `ServicoFormas`, no padrão de `DocaAmpliacao`); com o mouse sobre o botão, a opacidade
  dela anima para 0 e volta.

## Deixado para quem constrói

- A forma exata de ligar a camada cinza ao recurso (visibilidade por recurso dinâmico vs. trigger) desde que trocar a
  opção no perfil reflita na hora, sem reabrir a doca.
- O texto de ajuda sob a opção.
