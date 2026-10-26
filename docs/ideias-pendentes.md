# Ideias pendentes

Considerações do Gustavo que ainda não viraram spec. Cada uma passa por conversa de desenho antes de virar plano.

## Ler e responder ao Claude pelo notch (próxima fase, combinada em 26/10/2026)

- **Ver a mensagem inteira:** o aviso "Claude terminou" mostra só o começo da resposta. Uma rolagem no próprio notch,
  ou uma prévia da conversa, para ler tudo o que o Claude mandou.
- **Responder por texto:** digitar a resposta no notch. Caminho conhecido: no `Stop`, o gancho devolve
  `decision: "block"` com `reason` = o texto digitado. Ressalvas: a sessão aparece "trabalhando" enquanto o notch
  espera, e o texto entra como retorno do gancho, não como mensagem do usuário. Não existe gancho para mandar uma
  mensagem nova a qualquer momento.

## Notch "vivo" (25/10/2026)

- Notificações de outros apps chegando pelo notch e respostas por ali (precisa de identidade de pacote para ler as
  notificações).

## Calendário

- **Compromissos no calendário do notch** (Google ou Outlook): exige login na conta dentro do Vitral.

## Símbolo do Claude no notch (26/10/2026)

O notch usa um desenho do símbolo do Claude (marca da Anthropic) no aviso, na lateral e na ficha de permissão. Para
uso pessoal tudo bem; se o Vitral for distribuído para outras pessoas, trocar por um símbolo próprio.

## Já feito

- Painéis próprios e notch (barra do topo v2), notch vivo com avisos do Claude, faixa da doca no notebook (0.7.1).
- Barra do topo sem repetir bateria, volume e internet da barra de tarefas; logo novo.
- Atualização automática pelo GitHub (0.7.2), permissões do Claude pelo notch (0.8.0), painel da mídia ao passar o
  mouse (0.8.1).
