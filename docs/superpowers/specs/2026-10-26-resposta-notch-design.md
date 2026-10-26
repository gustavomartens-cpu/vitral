# Ler e responder ao Claude pelo notch

Data: 26/10/2026. Quando uma sessão do Claude Code termina, o notch mostra a resposta inteira do Claude e deixa o
Gustavo responder por texto ali mesmo. O que ficou sem resposta vai para uma lista de pendências.

## 1. Objetivo e escopo

O Gustavo roda várias sessões do Claude ao mesmo tempo e muitas vezes está fora do app (numa reunião, em outro
programa) quando elas terminam. Hoje o notch avisa "Claude terminou", mas mostra só o começo da resposta, e responder
exige ir até o app.

**Sucesso:** acompanhar e conduzir as sessões pelo notch, sem trocar de janela, nas respostas curtas e frequentes
("pode seguir", "faz a B", "corrige isso"). E não perder de vista as sessões que ficaram esperando.

**Base técnica** (conferida na documentação de ganchos do Claude Code, 26/10):

- O Claude Code não aceita uma mensagem nova de fora a qualquer momento.
- O caminho é o gancho `Stop`: ele pode segurar a sessão e devolver `{"decision":"block","reason":"<texto>"}`. Aí o
  Claude continua, com o texto como instrução.
- A entrada do `Stop` traz `session_id`, `transcript_path`, `cwd` e `stop_hook_active`.

**Fora desta fase:**

- responder pelo notch depois que a sessão já foi solta. As pendências servem para ler e abrir o app;
- abrir o app direto na conversa certa. O protocolo `claude://` existe, mas sem formato conhecido para isso;
- formatação rica (Markdown) na mensagem: ela aparece como texto simples;
- guardar as pendências em disco.

## 2. Escolhas do Gustavo

| Item | Escolha |
|------|---------|
| Uso | as duas coisas: responder na hora e, perdida a hora, uma lista de pendências |
| Pendências | ler a mensagem inteira e abrir o app do Claude (opção A); sem responder por ali |
| Quando segurar a sessão | só quando a janela do Claude não está na frente (abordagem 2) |
| Ficha | B de `notch-resposta.html`: compacta (duas linhas) com "ver tudo"; caixa de resposta embaixo |
| Lista | a de `notch-resposta.html`: "Pendências N" na lateral esquerda; lista descendo do notch |

## 3. O gancho e o mensageiro

### 3.1 Gancho

`GanchosClaude.Ligar` passa a escrever `"timeout": 1200` no item do `Stop`, para o Claude nunca cortar a espera no
meio. O `PermissionRequest` continua com 75, e o `Notification` sem timeout.

A caixa "Avisos do Claude no notch" aparece desmarcada enquanto o `Stop` não tiver o timeout novo. Marcar de novo
reescreve os três ganchos, sem duplicar.

### 3.2 Mensagem inteira

`AvisoClaude.UltimaRespostaDoAssistente` já acha a última mensagem de texto do assistente na transcrição. Dela saem
dois textos:

- **`Texto`:** o resumo de uma linha de hoje, com no máximo 80 caracteres. Continua servindo ao aviso curto e à
  lateral.
- **`Completo` (novo):** a mensagem inteira, sem o Markdown simples, com as quebras de parágrafo mantidas, até 32 KB
  (32.768 caracteres). Acima disso é cortada com "…".

O trecho lido da transcrição passa de 64 KB para os últimos 256 KB, para caber uma mensagem longa.

### 3.3 Segurar ou não

No `Stop`, o mensageiro decide:

1. **A janela da frente é do app do Claude** (`GetForegroundWindow` de um processo `claude`): manda o aviso de sempre,
   agora com o `Completo`, e sai. O Gustavo já está no app.
2. **Senão:** manda um `PedidoResposta` ao Vitral pelo canal e espera a resposta por até `Canal.EsperaRespostaMs` =
   1.140.000 ms (19 min, abaixo do timeout 1200).
   - **Chegou texto:** escreve `{"decision":"block","reason":"<texto>"}` na saída e sai.
   - **Sem texto** (conexão fechada sem resposta, tempo, Vitral fechado): não escreve nada. O `Stop` termina normal.

O prazo total do `--aviso` no `Stop` passa a ser de 19,5 min no caminho 2; o 1,5 s continua no caminho 1.

`stop_hook_active` verdadeiro, quando o Claude terminou de novo depois de uma resposta, não muda nada. A ficha volta
normal: cada volta exige o Gustavo digitar, então não há laço.

No `Notification` ("Claude está esperando você"), o mensageiro também lê o `Completo` da transcrição. Ele manda
`ForaDoApp = true` quando a janela da frente não é do Claude: só assim o aviso entra nas pendências.

### 3.4 Linhas do canal

- **`PedidoResposta`** (novo, no Core, no molde de `PedidoPermissao`): `Id`, `Sessao` (nome da pasta), `SessaoId`,
  `Completo` e `Hora`. Linha `{"tipo":"Resposta",...}`, com `DeLinha`, `ParaLinha` e `EhPedido`.
- **Resposta:** `{"id":"…","texto":"…"}`, com `RespostaTexto.Linha(id, texto)` e `RespostaTexto.DaLinha(linha,
  idEsperado)`. Texto vazio vale como sem resposta.
- **Saída para o Claude:** `RespostaTexto.ParaClaude(texto)` devolve `{"decision":"block","reason":texto}`.
- **`AvisoClaude`** ganha `Completo` e `ForaDoApp`, os dois opcionais.
- **Limite da linha:** `AvisoClaude.MaximoLinha` passa de 4 KB para 64 KB. Isso vale para os buffers do canal e para a
  leitura no `CanalAvisos`.

## 4. No Vitral

### 4.1 Regra (Vitral.Ui, testável)

O `EstadoNotch` ganha o modo `Resposta`, com fila própria. Cada item da fila tem o pedido e a hora de chegada.

- **Prioridade:** pedido de permissão vem antes de tudo. Ficha de resposta vem antes de aviso.
- **Prazo:** cada ficha conta `TempoResposta` = 60 s desde que chegou.
- **`ComecouADigitar(id)`:** para o tempo da ficha atual, que passa a valer só o teto `TetoDigitando` = 15 min,
  contado do primeiro caractere.
- **`Enviou(id, texto)`:** encerra com o texto (evento `RespostaEncerrada(pedido, texto)`). Mostra "✓ Enviado" por
  1 s, como a confirmação das permissões.
- **`Cancelou(id)`** (Esc), **tempo esgotado** ou **teto atingido:** encerra sem texto, e a sessão entra nas
  pendências.
- **`Desistiu(id)`** (o mensageiro fechou a conexão): sai sem entrar nas pendências. O aviso seguinte da sessão diz o
  que houve.
- **Aviso da mesma sessão** (pelo `SessaoId`): descarta a ficha dela, como nas permissões.
- **Clique logo depois de trocar:** a mesma `EsperaClique` de 0,5 s das permissões vale para Enviar.

**Pendências** (`Pendencias`, ao lado do `EstadoNotch`): lista em memória de `(SessaoId, Sessao, Tipo, Hora,
Completo)`, a mais nova em cima.

- **Entra:** ficha de resposta encerrada sem texto; aviso `Esperando` com `ForaDoApp`.
- **Sai:** "Abrir no Claude" no item; "Descartar" ou "×"; ou chegou aviso ou ficha nova da mesma sessão. O mais novo
  substitui o antigo.
- **Contagem:** `Quantas`, para a lateral.

### 4.2 Notch

**Ficha de resposta** (forma B de `notch-resposta.html`):

- largura até 460 px; topo com o símbolo do Claude, "Claude terminou", "pasta · hora" e "abrir no Claude ›";
- mensagem compacta em duas linhas, com esmaecido. "ver tudo ⌄", ou rolar o mouse em cima, abre até ~10 linhas
  roláveis (150 px);
- caixa de resposta com "Enviar". Enter envia, Shift+Enter quebra linha, Esc cancela;
- barra de 60 s, que para e fica cinza quando o Gustavo começa a digitar;
- a mesma janela do notch. Ela não pega foco (`WS_EX_NOACTIVATE`) para não roubar o teclado. Ao clicar na caixa de
  resposta, a janela tira o `WS_EX_NOACTIVATE` e se ativa. Ao enviar ou cancelar, devolve o estilo e o foco volta para
  onde estava.

**Lateral esquerda:** com pendências, mostra "Pendências N" no lugar do aviso, e o clique abre a lista. Sem
pendências, fica como hoje. "Pedido sem resposta" das permissões continua com prioridade sobre ela.

**Lista de pendências** (de `notch-resposta.html`):

- painel novo `Painel.Pendencias`, que desce do notch pelo `CoordenadorPaineis`, como o calendário;
- cada item mostra símbolo, pasta, "terminou" ou "esperando você", hora, a primeira linha e o "×";
- o clique no item abre a mensagem inteira (rolável, até 110 px) com "Abrir no Claude" e "Descartar";
- "Abrir no Claude" traz o app para a frente (`AbrirClaude.Trazer`) e tira o item.

### 4.3 Canal

O `CanalAvisos` trata `PedidoResposta` como trata `PedidoPermissao`:

- entrega ao notch e espera a decisão;
- responde na mesma conexão se houver texto;
- se o mensageiro fechar antes, avisa o `Desistiu`.

O limite de segurança do `WaitAsync` passa a ser 19,5 min nesse tipo.

**Sem notch visível** (barra do topo desligada, tela cheia): o pedido volta sem texto na hora e entra nas pendências.

## 5. Erros e bordas

| Caso | Comportamento |
|------|---------------|
| Vitral fechado ou travado | o mensageiro não conecta (300 ms) e solta a sessão |
| Digitando e saiu do PC | teto de 15 min: solta, entra nas pendências; o texto digitado se perde |
| Resposta digitada no app enquanto o notch segura | esperado: o app guarda a mensagem e, ao soltar (tempo ou cancelar), ela segue; a ficha sai com o aviso seguinte. Conferir no teste prático |
| Várias sessões juntas | fila; uma ficha por vez; o "+N" como nas permissões |
| Permissão chega com uma ficha de resposta aberta | a permissão passa à frente. A ficha volta depois com o tempo que restar; se já estava digitando, o texto fica guardado |
| Mensagem maior que 32 KB | cortada com "…" |
| Markdown | títulos, negrito, código e listas sem os símbolos; parágrafos mantidos |
| Texto enviado vazio | "Enviar" fica desligado com a caixa vazia |

## 6. Testes

**Com teste escrito antes:**

- **Core:**
  - `PedidoResposta` e `RespostaTexto`: ida e volta, id errado, texto vazio;
  - `ParaClaude` com aspas e quebras de linha;
  - `Completo`: Markdown limpo, parágrafos mantidos, corte em 32 KB;
  - `GanchosClaude`: timeout 1200 no `Stop`; a caixa desmarcada sem ele; ligar de novo sem duplicar.
- **Ui (relógio falso):**
  - fila e 60 s, com o tempo que para ao digitar e o teto de 15 min;
  - enviar, cancelar e desistir;
  - prioridade da permissão e retomada com o texto guardado;
  - descarte pela mesma sessão e `EsperaClique`;
  - `Pendencias`: entra, sai e o mais novo substitui o antigo.

**Na prática, com o Gustavo:**

1. Com o app em segundo plano, uma sessão termina; responder pelo notch; o Claude continua com o texto.
2. Deixar passar os 60 s; a sessão vai para as pendências; abrir pelo item.
3. Com o app do Claude na frente, a sessão termina e não fica presa ("trabalhando").
4. Duas sessões terminando juntas; a segunda espera na fila.

## 7. Riscos

- **Foco do teclado:** ativar a janela do notch (hoje sem foco) para digitar e devolver o foco depois. Precisa de
  conferência cuidadosa na tela.
- **App com a sessão presa:** enquanto o notch espera, a sessão aparece "trabalhando" no app. A regra da janela da
  frente reduz isso, mas não elimina (app aberto atrás de outra janela).
- **Formato do `Stop` com `block`:** a resposta entra como retorno do gancho, não como mensagem do Gustavo. Na
  transcrição ela aparece diferente de uma mensagem digitada no app.
