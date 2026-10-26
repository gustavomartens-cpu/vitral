# Permissões do Claude pelo notch

Data: 26/10/2026. O notch passa a mostrar os pedidos de permissão do Claude Code ("Claude quer rodar um comando") com
Permitir, Sempre nesta sessão e Negar. A resposta volta para o Claude pelo mesmo canal dos avisos.

## 1. Objetivo e escopo

O Gustavo quer responder aos pedidos de permissão sem ir até a janela do Claude. Esta é a primeira parte de
"Responder ao Claude pelo notch" (`docs/ideias-pendentes.md`). A resposta por texto no `Stop` fica para um experimento
depois.

Base técnica: o gancho `PermissionRequest` do Claude Code, conferido na documentação em 26/10.

- **Entrada:** `tool_name`, `tool_input`, `permission_suggestions`, `cwd`, `session_id` e `hook_event_name`.
- **Saída:** `hookSpecificOutput.decision` com `behavior` (`allow` ou `deny`), mais `updatedPermissions` no allow e
  `message`/`interrupt` no deny.
- **Concorrência:** no app do Claude (Agent SDK), o gancho roda junto com o pedido do próprio app, e vale quem decidir
  primeiro.
- **Sem decisão:** se o gancho não devolve nada, o fluxo normal de permissão segue.

## 2. Escolhas do Gustavo

| Item | Escolha |
|------|---------|
| Respostas | Permitir · Sempre nesta sessão · Negar |
| Tempo no notch | 60 s |
| Forma | `notch-permissao.html`, A: ficha alta (112 px) com o pedido em cima e os botões embaixo |
| Volta da resposta | o mesmo canal, de mão dupla (a conexão fica aberta esperando a resposta) |

## 3. O gancho e o mensageiro

### 3.1 Gancho novo

`GanchosClaude.Ligar` passa a escrever três eventos:

- `Stop` e `Notification`, como hoje;
- `PermissionRequest`, com `"timeout": 75` no item do comando.

O tempo-limite fica acima dos 60 s do notch, para o Claude nunca cortar a espera no meio.

`EstaLigado` exige os três. Quem tinha só os dois antigos aparece como desligado. Marcar a caixa de novo acrescenta o
terceiro sem duplicar os outros, porque `Ligar` já troca os nossos itens.

### 3.2 Pedido (`PedidoPermissao`, no Core)

`PedidoPermissao.DeGancho(json) → PedidoPermissao?` monta o pedido a partir do que o gancho recebe:

- `Id`: um GUID novo, em texto;
- `Sessao`: o nome da última pasta de `cwd`, como nos avisos;
- `Ferramenta`: o `tool_name`;
- `Titulo` e `Resumo`, conforme a ferramenta:

| `tool_name` | Título | Resumo |
|-------------|--------|--------|
| `Bash` | Claude quer rodar um comando | `tool_input.command` |
| `Edit`, `MultiEdit` | Claude quer editar um arquivo | nome de `tool_input.file_path` |
| `Write` | Claude quer criar um arquivo | nome de `tool_input.file_path` |
| `WebFetch` | Claude quer abrir um endereço | `tool_input.url` |
| começa com `mcp__` | Claude quer usar uma ferramenta | o último trecho depois de `__` |
| outra | Claude quer usar uma ferramenta | o próprio `tool_name` |

O resumo vai numa linha só, com no máximo 200 caracteres. Se faltar o campo, o resumo é o nome da ferramenta.

Evento que não é `PermissionRequest`, JSON vazio ou com defeito devolvem null.

### 3.3 Resposta para o Claude (`RespostaPermissao`, no Core)

`enum Escolha { Permitir, SempreNaSessao, Negar }`

`RespostaPermissao.ParaClaude(Escolha, string jsonDoGancho) → string` monta o JSON da saída:

- **Permitir:** `{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"allow"}}}`.
- **SempreNaSessao:** o mesmo allow, com `updatedPermissions` contendo uma entrada `addRules` com
  `"destination":"session"` e `"behavior":"allow"`. As regras vêm daqui:
  1. as `rules` da primeira sugestão de `permission_suggestions` com `type` igual a `addRules` e `behavior` igual a
     `allow`;
  2. sem sugestão, no `Bash`: `[{"toolName":"Bash","ruleContent":<command>}]`;
  3. sem sugestão nas demais ferramentas: `[{"toolName":<tool_name>}]`.
- **Negar:** `decision` com `"behavior":"deny"`, `"message":"Negado no notch do Vitral."` e `"interrupt":false`.

### 3.4 Mensageiro

No `--aviso`, se o evento for `PermissionRequest`:

1. Monta o `PedidoPermissao` e o envia pelo canal como linha
   `{"tipo":"Permissao","id":…,"sessao":…,"ferramenta":…,"titulo":…,"resumo":…}`.
2. Espera na mesma conexão uma linha `{"id":…,"escolha":"Permitir|SempreNaSessao|Negar"}` por até 60 s. Resposta com
   outro `id`, ou linha inválida, é ignorada e conta como sem resposta.
3. Com a escolha, escreve na saída padrão o JSON de `ParaClaude` e sai com 0.
4. Sem resposta (tempo, Vitral fechado ou conexão caída), sai com 0 sem escrever nada. O Claude segue o fluxo normal.

O prazo total do `Main` passa de 1,5 s para 65 s, mas só no `PermissionRequest`. Os outros eventos continuam com 1,5 s.

### 3.5 Canal de mão dupla

- **Cliente:** `Canal.Perguntar(nomeCanal, linha, esperaMs) → string?`, no Core, conecta como `InOut`, escreve a
  linha e lê uma linha de resposta até o prazo. Em qualquer falha, devolve null.
- **Servidor (`CanalAvisos`):**
  - até 4 instâncias, cada conexão tratada numa tarefa própria, com a mesma ACL do usuário e buffers de 4 KB;
  - linha de aviso: entrega ao notch e fecha, como hoje;
  - linha de permissão: entrega ao notch com um `TaskCompletionSource<Escolha?>`, espera a escolha, escreve a linha de
    resposta e fecha;
  - escolha null (tempo esgotado ou pedido descartado): fecha sem escrever.

## 4. O notch

### 4.1 Estado de pedido

`ModoNotch` ganha `Permissao`. As medidas vão para `NotchMedidas`: altura 112, largura até 420, raio 24.

Conteúdo, de cima para baixo:

- **Linha 1:** ícone do Claude (22 px), título em negrito 12 e, embaixo, `sessão · ` seguido do resumo em fonte de
  código (`Cascadia Mono, Consolas`, corpo 11, cor `#FFD88A`, fundo `#12FFFFFF`, cortado com "…"). O resumo inteiro
  fica na dica do mouse.
- **Linha 2:** três botões em pílula de 28 px, com o raio pela meia altura:

  | Botão | Aparência | Hover |
  |-------|-----------|-------|
  | Permitir | `AmbarGradiente`, texto `TextoSobreAmbar` em negrito | — |
  | Sempre nesta sessão | `#12FFFFFF` | `#22FFFFFF` |
  | Negar | `#12FFFFFF` | `#38E85454`, texto `#FF8A8A` |

  Os três afundam no clique (`Efeitos.Afundar`).
- **Linha 3:** barra de 2 px, `Ambar`, que encolhe da direita para a esquerda até o fim dos 60 s. Com a animação do
  Windows desligada, ela fica parada e cheia.
- **Fila:** com mais pedidos esperando, um "+N" em `TextoSecundario`, corpo 10, no canto de cima à direita.

**Depois do clique:**

- a ficha mostra por 1 s, no lugar dos botões, "✓ Permitido", "✓ Sempre nesta sessão" ou "Negado" (este em
  `#FF8A8A`);
- depois entra o próximo pedido da fila, ou o notch volta ao normal.

### 4.2 Regras (`EstadoNotch`)

**Entradas novas:**

- `Pediu(PedidoPermissao p)`: entra na fila. Se nada estiver aberto, vira o pedido atual, com o modo `Permissao`.
- `Respondeu(Escolha e)`: devolve o pedido respondido, para o canal completar a resposta. Depois mostra a confirmação
  e passa ao próximo.
- No `Tique`:
  - o pedido atual com mais de 60 s desde que **ele chegou** sai sem resposta: ele completa com null;
  - os pedidos da fila com mais de 60 s também saem.

  Quando o último sai sem resposta, o modo vai para `Lembrete`, com `Pendente`, e a lateral esquerda mostra "Pedido
  sem resposta".
- `Chegou(aviso)` de uma sessão com pedido aberto ou na fila: descarta os pedidos dessa sessão, que completam com null,
  porque foram resolvidos no app. Depois segue a regra do aviso.

**Prioridade:** enquanto há pedido aberto, um aviso de outra sessão não toma o notch. Ele vira lembrete, e a lateral
esquerda o mostra depois.

**Mouse:** em cima da ficha, ele não segura o tempo do pedido. Os 60 s são do Claude.

**Saída nova:** `event Action<PedidoPermissao, Escolha?> Encerrado`, ouvido pelo canal. Dispara uma vez por pedido,
com a escolha ou null.

## 5. Testes

**Com teste escrito antes:**

- `PedidoPermissao.DeGancho`:
  - um caso por linha da tabela do §3.2;
  - campo ausente;
  - comando com quebras de linha vira uma linha só;
  - resumo cortado em 200;
  - evento errado, vazio ou com defeito devolvem null.
- `RespostaPermissao.ParaClaude`:
  - os três botões;
  - "sempre" com sugestão, sem sugestão no Bash e sem sugestão noutra ferramenta;
  - o JSON tem `hookEventName` e o `destination` é `session` mesmo quando a sugestão traz outro destino.
- Protocolo:
  - linha do pedido, ida e volta;
  - resposta com o mesmo id;
  - resposta de outro id ignorada;
  - linha maior que 4 KB recusada.
- `Canal.Perguntar`:
  - um servidor de teste responde e chega a resposta;
  - um servidor que não responde dá null no prazo;
  - sem servidor, dá null rápido.
- `GanchosClaude`:
  - ligar escreve os três eventos, com `"timeout": 75` no `PermissionRequest`;
  - um arquivo com só os dois antigos conta como desligado, e ligar acrescenta o terceiro sem duplicar;
  - desligar tira os três.
- `EstadoNotch`:
  - pedido abre `Permissao`;
  - responder dispara `Encerrado` com a escolha e volta ao normal depois da confirmação;
  - 60 s sem resposta dispara `Encerrado` com null e vai ao lembrete;
  - dois pedidos: fila com "+1", o segundo entra depois do primeiro, e o tempo do segundo é contado desde que ele
    chegou;
  - aviso da mesma sessão descarta o pedido (null);
  - aviso de outra sessão durante um pedido não toma o notch;
  - mouse em cima não segura o tempo.

**Na tela, com o Gustavo:**

- um pedido real deste Claude, respondido pelo notch nos três jeitos, conferindo o efeito: o comando roda, a regra de
  sessão vale no pedido seguinte igual, e o Claude recebe a negação;
- um pedido respondido no app, para ver o notch limpar quando chega o próximo aviso daquela sessão;
- o tempo esgotando.

**Conferência no Claude Code de terminal:** se o pedido dele aparece enquanto o gancho espera. O resultado fica
registrado no ledger.

## 6. Riscos

- **Pedido nos dois lugares:** no app do Claude, ele aparece no app e no notch. Respondido no app, o pedido do notch
  só sai com o próximo aviso daquela sessão ou com o fim dos 60 s.
- **Claude Code de terminal:** se ele não mostrar o próprio pedido enquanto o gancho espera, ali só o notch responde
  por até 60 s. Se a conferência mostrar isso, a escolha fica com o Gustavo: um filtro só para o app, ou um tempo
  menor.
- **"Sempre nesta sessão":** sem sugestão do Claude, numa ferramenta que não é o Bash, a regra libera a ferramenta
  inteira na sessão. A sugestão do próprio Claude, quando vem, é mais estreita e tem prioridade.
- **Formato do Claude Code:** pode mudar. Os testes fixam o de hoje, e o mensageiro nunca falha para o Claude.

## 7. Fora desta fase

- A resposta por texto no `Stop`.
- O "Sempre" gravado nas configurações do projeto.
- O filtro por ferramenta.
