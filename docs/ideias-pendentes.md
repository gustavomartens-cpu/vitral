# Ideias pendentes

Considerações do Gustavo que ainda não viraram spec. Cada uma passa por conversa de desenho antes de virar plano.

## Próximos projetos (escolhidos pelo Gustavo em 27/10/2026)

- **Agenda da Microsoft:** cadastrar o app no Azure (`docs/agenda-cadastro.md`) e pôr o ID em `Credenciais.cs`; conferir
  o dia inteiro do Outlook na tela.
- **Pequenos acertos da agenda** (revisão final da 0.12.0): clique num aviso do Claude deixa o lembrete da agenda vivo
  sem o ponto; compromisso preso na fila atrás de ficha longa pode aparecer já terminado; login do Google lento ou com a
  API desligada sem mensagem clara; login da Microsoft sem limite de tempo; escolher outras agendas além da principal.
- **Notificações de outros apps no notch**, e respostas por ali: precisa de identidade de pacote para ler as
  notificações do Windows.
- **Pequenos acertos da aparência** (revisões finais da 0.10.0 e da 0.11.0): não recalcular quando o Windows avisa sem
  mudar nada; tirar os avisos de tema antigos das ilhas, painéis e doca; prévia das abas acompanhar o Windows e a troca
  de papel de parede; cor fora da paleta aparecer no seletor; enum novo no perfil exige subir o esquema; raio do notch
  na prévia seguir os cantos da aparência; barra única com mídia e status desligadas mostra uma faixa vazia.
- **Pequenos acertos da janela** (revisão final da 0.13.0): contorno de foco invisível nas janelas das partes com tema
  claro; mensagem longa cortada e que some em 6 s mesmo com a janela escondida; perfil que some com alteração pendente
  (Novo, Importar); botão de vidro com pouco contraste sobre cartão; nome do perfil comparado com maiúsculas na lista.
  Anteriores à repaginação: falha ao carregar mantém o editor antigo; Importar sobre o perfil aberto não recarrega;
  Sair pela bandeja perde a edição não salva sem perguntar.

## Testes a fundo pendentes

As opções do perfil têm testes da regra, mas nunca foram conferidas de ponta a ponta no Windows: cores e tema, barra
de tarefas (ocultar automaticamente, selos, alinhamento, pesquisa, visão de tarefas, agrupar, todos os monitores,
piscar, ícones perto do relógio), e "Testar (20 s)", "Desfazer" e "Voltar ao Windows original".

**Opções da barra do Windows (29/10/2026):** com a doca ligada, a barra do Windows fica escondida e as opções dela não
têm onde aparecer — por isso o "ocultar automaticamente" parecia não funcionar. Desde a 0.13.0 a aba as recolhe com a
doca ligada. Falta conferir cada uma **com a doca desligada**, num perfil de teste, com o Gustavo olhando a barra; as
que não fizerem efeito saem da tela (`Visibility="Collapsed"`) e entram aqui com o observado.

## Descartado

- Responder às pendências pelo notch (27/10/2026): a pendência existe para abrir a conversa no app, com tempo para
  rever.

## Símbolo do Claude no notch (26/10/2026)

O notch usa um desenho do símbolo do Claude (marca da Anthropic) no aviso, na lateral e na ficha de permissão. Para
uso pessoal tudo bem; se o Vitral for distribuído para outras pessoas, trocar por um símbolo próprio.

## Já feito

- Painéis próprios e notch (barra do topo v2), notch vivo com avisos do Claude, faixa da doca no notebook (0.7.1).
- Barra do topo sem repetir bateria, volume e internet da barra de tarefas; logo novo.
- Atualização automática pelo GitHub (0.7.2), permissões do Claude pelo notch (0.8.0), painel da mídia ao passar o
  mouse (0.8.1), consertos (0.8.2), ler e responder ao Claude pelo notch com lista de pendências (0.9.0), Descartar na
  ficha (0.9.3), abrir a conversa certa no app do Claude (0.9.4), notch certo depois de trocar de escala
  (0.9.5), aparência da doca, do
  notch e das ilhas com sete temas, personalizar e diferenciar (0.10.0), formas da doca, do notch e das ilhas e
  ícone da bateria pelo nível (0.11.0), compromissos do Google Agenda no notch: lista do dia,
  pontinhos na grade, aviso antes com Entrar (0.12.0), janela repaginada em vidro fumê, abrindo no perfil ativo,
  com abas por parte da tela e Configurações na engrenagem (0.13.0), fixado de app da loja que sobrevive à
  atualização do app (0.13.2).
