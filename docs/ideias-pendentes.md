# Ideias pendentes

Considerações do Gustavo que ainda não viraram spec. Cada uma passa por conversa de desenho antes de virar plano.

## Próximos projetos (escolhidos pelo Gustavo em 27/10/2026)

- **Agenda da Microsoft:** cadastrar o app no Azure (`docs/agenda-cadastro.md`) e pôr o ID em `Credenciais.cs`; conferir
  o dia inteiro do Outlook na tela.
- **Escolher agendas da Microsoft** (depois do cadastro no Azure).
- **Notificações de outros apps no notch**, e respostas por ali: precisa de identidade de pacote para ler as
  notificações do Windows.
- **Sobras da aparência** (revisão final da 0.13.7): papel de parede no mesmo caminho (apresentação, Spotlight) não
  relê por causa do cache do WPF (`IgnoreImageCache`); a prévia da aba abre com o Windows padrão até a primeira mudança
  (chamar `EditorAparencia.Recalcular()` depois de ligar o `LerWindows`); barra única com mídia ligada e nada tocando
  ainda mostra a faixa; a prévia das formas sempre desenha a faixa da barra única; opção desconhecida some ao regravar
  ou exportar o perfil (fixar em teste ou preservar); troca de tema sem mudar valores não reaplica o vidro (só se o
  acrílico sumir); `Loaded` duplo da prévia assina o serviço duas vezes; o "Personalizada" some ao escolher outra cor.
- **Erro na troca de sessão de mídia:** `NullReferenceException` em `FonteMidiaWindows.TrocarSessao` no log (01/11).

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
  atualização do app (0.13.2), pequenos acertos da janela: foco no tema claro, mensagem inteira e que espera, pergunta
  antes de criar, importar e sair com alteração pendente, falha ao abrir volta ao anterior (0.13.4), outras agendas do Google, login da agenda com
  mensagens claras e Cancelar, e o notch sem pedir resposta da sessão aberta no Claude em qualquer monitor (0.13.5),
  barras, notch e doca somem com jogo em tela cheia mesmo sem o aviso do Windows (0.13.6), prévia acompanhando o
  Windows e o papel de parede, cor personalizada no seletor, opção desconhecida vira "Não mexer", raio do notch na
  prévia e barra única sem faixa vazia (0.13.7), sessão à vista pela parte descoberta da janela do Claude, Importar
  perguntando "Substituir?" antes, Descartar com perfil que falha, Sair e reiniciar perguntando com a janela à vista,
  mensagem parada com a janela minimizada e login de 5 min explicado (0.13.8).
