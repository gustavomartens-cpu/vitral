# Perguntas do Claude no notch

Data: 2026-11-03 · Aprovado na conversa com o Gustavo · Base: spec das permissões
(`docs/superpowers/specs/2026-10-26-permissoes-notch-design.md`)

## 1. O que é

Quando o Claude faz uma pergunta de escolha (`AskUserQuestion`), o Claude Code pede permissão para a ferramenta. O gancho
`PermissionRequest` do Vitral recebe esse pedido. Hoje ele vira a ficha "Claude quer usar uma ferramenta"
(Permitir / Sempre / Negar), e permitir não responde a pergunta: o Gustavo sempre volta à conversa. Com esta etapa, o
notch mostra a pergunta com as opções, e a escolha volta ao Claude como a resposta.

**Sucesso:** uma pergunta de escolha numa sessão do app do Claude aparece no notch com as opções. Um clique (ou as
marcações e "Enviar") responde, a conversa segue com a resposta e a pergunta não fica esperando no app. Responder no
app primeiro tira a ficha do notch. "Responder na conversa" leva ao app com a pergunta esperando.

## 2. O que a prova mostrou (03/11/2026)

Claude Code 2.1.318, sessão interativa isolada com um gancho de teste:

- O pedido chega com `tool_name = "AskUserQuestion"` e `tool_input.questions[]`: cada uma com `question`, `header`,
  `options[]` (`label`, `description`) e `multiSelect`.
- Devolver `{"hookSpecificOutput":{"hookEventName":"PermissionRequest","decision":{"behavior":"allow","updatedInput":
  {"questions":[…as mesmas…],"answers":{"<texto da pergunta>":"<rótulo>"}}}}}` responde a pergunta. O Claude Code
  registra "User answered Claude's questions… Allowed by PermissionRequest hook" e o modelo recebe a resposta.
- Várias escolhas: os rótulos juntos com ", " ("Azul, Verde"). Várias perguntas na mesma chamada: todas no mesmo
  `answers`.
- A pergunta aparece no app enquanto o gancho espera; quem responder primeiro vale. Responder no app encerra o gancho
  (o mensageiro fecha o canal), como já acontece com as permissões.
- Não visto ainda: a mesma coisa no app do desktop (o mesmo motor; a primeira passada na máquina confere).

## 3. A ficha de pergunta no notch

- **Cabeçalho:** "Claude pergunta" e a sessão (o nome da pasta, como nas permissões).
- **Pergunta:** o texto inteiro, até duas linhas (o resto com reticências; inteiro na dica do mouse).
- **Opções:** um botão por opção com o `label` como veio (inclusive "(Recomendado)"); a `description` na dica do mouse.
  Escolha única: o clique responde. Várias escolhas: os botões marcam e desmarcam, e "Enviar" (desligado sem nenhuma
  marcada) responde.
- **Várias perguntas:** uma de cada vez, com "1/2" no canto; as escolhas se acumulam e vão juntas na última.
- **"Responder na conversa":** sempre na ficha. Fecha a ficha sem resposta (o Claude Code segue com a pergunta no app) e
  traz o app do Claude para a frente, na conversa da sessão quando o id dela é conhecido (o mesmo caminho do clique nos
  avisos do Claude).
- Sem Permitir, Sempre e Negar.
- **Não cabe:** uma pergunta sem opções, com mais de 4 opções ou com mais de 4 perguntas mostra só o cabeçalho, a
  primeira pergunta e "Responder na conversa".

## 4. Tempo

- A ficha de pergunta fica até **5 minutos** (`TempoPergunta = 300`); a de permissão continua com 60 s.
- O gancho `PermissionRequest` passa de 75 s para **330 s** no `settings.json` do Claude (`GanchosClaude`), e o prazo do
  `--aviso` para pergunta passa a 310 s (permissão continua 65 s). Na prova, a pergunta continuou respondível no app
  enquanto o gancho esperava; uma permissão comum também continua respondível no app.
- Os ganchos já ligados com o tempo antigo são religados pelo caminho que já existe (o `EstaLigado` passa a exigir o
  tempo novo).

## 5. O canal

- `PedidoPermissao` ganha `Perguntas` (nulo fora de `AskUserQuestion`): `Pergunta(Texto, Cabecalho, Opcoes, Multipla)`
  e `Opcao(Rotulo, Descricao)`. A linha do canal leva as perguntas; se a linha passaria do limite, vai sem elas e a
  ficha cai no "não cabe".
- A resposta do canal ganha `Respostas` (pergunta → texto) além da `Escolha`; `EscolhaPermissao` ganha `Conversa`
  ("Responder na conversa").
- `RespostaPermissao.ParaClaude`: com respostas, `allow` com `updatedInput` = as perguntas originais do gancho e
  `answers`; com `Conversa` ou sem resposta, nada é escrito (o Claude Code segue com a pergunta no app).

## 6. Peças

| Peça | Onde | O que faz |
|---|---|---|
| `Pergunta`, `Opcao`, `PedidoPermissao.Perguntas`, `RespostaPermissao` com respostas, `RegrasPerguntas` (cabe?, montar `answers`) | Vitral.Core/Claude | Regras puras e o JSON |
| `GanchosClaude` | Vitral.Core/Claude | Tempo de 330 s |
| Modo `Pergunta` no `EstadoNotch` | Vitral.Ui/Barra | Pergunta atual, marcações, avançar, enviar, tempo de 5 min |
| Ficha de pergunta | Vitral.App/Barra/JanelaNotch | Desenho e cliques; "Responder na conversa" traz o app |
| `Mensageiro` | Vitral.App/Claude | Prazo de 310 s para pergunta |

## 7. Erros

- Vitral fechado, notch fora da tela ou tempo esgotado: sem resposta, o Claude Code segue com a pergunta no app.
- JSON do gancho com formato inesperado (sem `questions`, opção sem `label`): vira a ficha "não cabe".

## 8. Testes

- Core: ler as perguntas do gancho (única, múltipla, várias, acentos); "não cabe"; montar a saída com `answers` e as
  perguntas originais; `Conversa` e nulo não escrevem nada; tempo novo do gancho e religar o antigo.
- Ui: modo Pergunta (clique responde; marcar, desmarcar e Enviar; 1/2 e acumular; tempo de 5 min; sair quando o Claude
  desiste; não cabe).
- Na máquina: uma pergunta real do Claude neste app respondida pelo notch.

## 9. Fora desta etapa

Digitar "Outra resposta" no próprio notch; perguntas com prévia (`preview`) mostradas no notch.
