# Ideias pendentes

Considerações do Gustavo que ainda não viraram spec. Cada uma passa por conversa de desenho antes de virar plano.

## Barra do topo × barra de tarefas (25/10/2026)

- **Sem informação duplicada.** Bateria, volume e internet não devem aparecer ao mesmo tempo na barra do topo e na
  área de notificação da barra de tarefas. Decidir na fase da barra de tarefas: o que fica em cima e o que sai de baixo.
- **Painéis próprios e notch:** viraram a spec `docs/superpowers/specs/2026-10-25-barra-do-topo-v2-design.md`.
- **Compromissos no calendário do notch** (Google ou Outlook): fora da v2; exige login na conta dentro do Vitral.

## Notch "vivo" (25/10/2026)

- Notificações chegando pelo notch e respostas por ali (precisa de identidade de pacote para ler notificações de
  outros apps).
- Atalho para o Claude no notch: avisos do Claude Code (tarefa terminada, pergunta esperando resposta) e, se possível,
  responder por ali; ou um chat pela API da Anthropic.
- Cantos curvos ("orelhas") ligando o notch à borda de cima; provavelmente exige notch escuro sólido, sem acrílico.

## Logo em preto e branco (25/10/2026)

O Gustavo não gosta do azul com âmbar no logo do Vitral (doca e topo). Quer um logo preto e branco, ou só preto. Mudar junto com o próximo ajuste visual.

## Consertos pendentes

- **Notebook com tela menor (25/10/2026):** com a 0.6.0 instalada noutro notebook, sobra uma faixa vazia grande entre as
  janelas maximizadas e a doca (print do Gustavo). Suspeitas: a barra do Windows continuar reservando o espaço dela
  além dos 62 px da doca (ocultar automático não aplicado nessa máquina), ou a reserva multiplicada pela escala da tela.
  Diagnosticar nessa máquina com o estado da barra (visível/oculta, área de trabalho, ocultar automático, DPI).

## Responder ao Claude pelo notch (próxima fase, combinada em 25/10/2026)

Começar pelas **permissões**: gancho `PermissionRequest` do Claude Code. O notch mostra o pedido ("Claude quer rodar:
npm test") com Permitir / Negar; o mensageiro espera o clique (ex.: 60 s, `timeout` do gancho) e devolve
`hookSpecificOutput.decision.behavior` = allow/deny. Sem resposta a tempo, não devolve nada e o Claude mostra o pedido
normal dele.

Depois, como experimento: **resposta por texto** no `Stop` (`decision: "block"` + `reason` com o texto digitado no
notch). Ressalvas: a sessão aparece "trabalhando" enquanto o notch espera, e o texto entra como retorno do gancho, não
como mensagem do usuário. Não existe gancho para mandar uma mensagem nova a qualquer momento.

## Atualização automática pelo GitHub (pedido em 25/10/2026)

O Vitral confere as releases do GitHub (ao abrir e a cada poucas horas, com internet), baixa só o pacote delta do
Velopack e aplica ao reiniciar, ou avisa no notch para o Gustavo escolher a hora. A publicação passa a usar
`vpk upload github` (sobe Setup, Portable, os .nupkg e o releases.win.json) em vez de só o Setup e o Portable.

Decisão pendente: o repositório do código é privado, e o app instalado não baixa de repositório privado sem
credencial. Opções: (1, recomendada) um repositório público só com as releases (ex.: `vitral-releases`), com o código
continuando privado; (2) tornar o repositório do código público; (3) token só de leitura embutido no app (vai junto
em cada instalação e pode ser extraído).
