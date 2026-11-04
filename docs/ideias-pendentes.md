# Ideias pendentes

Considerações do Gustavo que ainda não viraram spec. Cada uma passa por conversa de desenho antes de virar plano.

## Próximos projetos (escolhidos pelo Gustavo em 27/10/2026)

- **Sobras das perguntas no notch** (revisão final):
  - **Altura da ficha:** a ficha de pergunta sobra uns 22 px embaixo, porque a margem é medida duas vezes.
  - **Pergunta fora do formato:** um item que não é objeto lança exceção e a pergunta nem chega ao notch (o certo é
    cair no "não cabe").
  - **Repetições:** perguntas de texto igual ou rótulos iguais se confundem (fazer o "cabe" recusar).
  - **Prazo do `--aviso`:** é decidido procurando o texto "AskUserQuestion"; seria melhor decidir pelo pedido lido.
  - **Ganchos antigos:** entre instalar e o `Atualizar` rodar, o Stop deixa de segurar a resposta pelo notch.
  - **Comentários:** os de 75 s e 60 s ficaram velhos.
  - **Testes:** falta o de ida e volta pelo canal de uma resposta com aspas.
  - **Botões:** são refeitos a cada marcação e perdem o destaque do mouse.

- **Sobras do Iniciar** (revisão final da 0.14.0): o log da DLL ganha 5 a 15 linhas "REPINTADO" a cada abertura,
  gravadas durante a animação; a leitura das cores espera até 250 ms na thread do Iniciar quando o arquivo some; o
  caminho das cores é regravado sem trava numa reconexão; os logs por processo nunca são apagados; uma cópia
  interrompida da DLL não é refeita; falta teste do retrato original sem a seção "iniciar"; a pasta dos logs aceita
  escrita de outros apps isolados (só mudaria a linha de estado).
- **Fones:** a prova de conectar um fone (QCY ou JBL) pelo painel de controles espera o Gustavo ligar um.
- **Sobras dos controles, segunda rodada** (revisão das sobras):
  - **Clique no meio de uma leitura:** uma leitura antiga que termina durante o clique faz o botão voltar ao estado
    anterior por 1 a 2 s (avançar a geração no início de cada comando).
  - **Nome das telas:** "Monitor N" segue a ordem geral, e a tela vertical aparece como "Monitor 2". Usar o número da
    saída de vídeo ou o nome real do monitor.
  - **Bluetooth:**
    - um aparelho com o LE conectado e o áudio clássico não conectado aparece "conectado", e o clique tenta
      desconectar o áudio;
    - se o endereço LE for diferente do clássico, o mesmo aparelho aparece duas vezes (juntar por nome e tipo como
      último recurso; conferir no aparelho).
  - **Microfone:** o mudo feito pelo botão do headset de comunicações não aparece no painel até o próximo clique.
  - **Memória:** ainda falta liberar o COM no `FonesBluetooth.Pedir` e no `NaoPerturbe.Criar`, e chamar o `Dispose` dos
    aparelhos Bluetooth.
- **Brilho sem DDC/CI (plano B):** o monitor principal do Gustavo (Odyssey, 2560×1440, por USB-C → DisplayPort) não
  responde ao DDC/CI (erro `0xC02625D8` já na leitura). Ver se é o caminho do cabo ou o menu do monitor; se não houver
  jeito, escurecer pela imagem (rampa de cores da placa), só abaixo do brilho atual.
- **Iniciar, próximas etapas** (spec do Iniciar §7): central de notificações e configurações rápidas, busca, textos e
  ícones do Iniciar com as cores da doca, assinatura digital da DLL.
- **Agenda da Microsoft:** cadastrar o app no Azure (`docs/agenda-cadastro.md`) e pôr o ID em `Credenciais.cs`; conferir
  o dia inteiro do Outlook na tela.
- **Escolher agendas da Microsoft** (depois do cadastro no Azure).
- **Sobras das notificações, segunda rodada** (revisão das sobras):
  - **Ícone:** um ícone que falha sempre é tentado e vai para o log a cada exibição (tentar de novo só depois de um
    tempo), e o aviso e o painel podem carregar o mesmo ícone duas vezes.
  - **Aviso:** começa com " · " quando o app não tem nome. A rajada mostra o ícone do primeiro app e "abrir ›"; o
    certo seria um sino e "ver ›".
  - **Testes:** a ordem da saída não tem teste.
  - **Brilho:** a tela do notebook recusando o ajuste enche o log a cada 100 ms.
- **Notificações, limites conhecidos** (sobras da 0.15.0, sem saída): app calado pelo Vitral que o usuário desliga
  de vez nas Configurações volta ligado na saída (o Windows grava o mesmo valor, não dá para distinguir); com o Vitral
  nunca mais aberto (sem desinstalar), o balão fica calado (só a desinstalação devolve).
- **Notificações, depois:** responder e os botões próprios das notificações (o Windows não deixa outro app acioná-los);
  assinatura do pacote para funcionar sem o modo de desenvolvedor.
- **Sobras da 0.13.9** (revisão final): barra única só com a mídia fecha e recria a faixa quando a sessão some e volta
  (jogo, navegador entre vídeos), com uma piscada da ilha sem fundo (chamar `Posicionar` antes da visibilidade,
  esconder em vez de fechar, juntar as rajadas); erro no meio da assinatura da sessão de mídia deixa um evento preso;
  as chamadas ao Windows dentro do lock da mídia podem segurar a interface (trocar por geração se aparecer no log);
  a prévia fechada sem `Unloaded` fica presa ao serviço.
- **Sobras da janela e da agenda** (revisão final da 0.13.8): a pergunta de salvar fica presa à janela principal mesmo
  com Configurações aberta, e um segundo clique em "reiniciar" abre outra por cima (usar a janela ativa como dono e
  travar a reentrada em `App.PodeFechar`); dono minimizado pode esconder a caixa (filtrar `WindowState`); "Importado: B"
  sem dizer que A continua aberto quando a troca é cancelada; Salvar na pendência do perfil que vai ser substituído
  confunde (perguntar "a edição será perdida"); comentário do login da Microsoft promete que fechar o navegador avisa;
  cobertura do Claude conta a borda invisível do DWM e a parte fora da tela; testes do Importar sem conflito com
  Cancelar/Descartar, conflito com Salvar/Descartar, arquivo inexistente com pendência e bordas da cobertura.

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
  mensagem parada com a janela minimizada e login de 5 min explicado (0.13.8), faixa da barra única só com a mídia
  tocando, cor personalizada guardada, prévia com o Windows real e o papel de parede relido, erro da troca de sessão
  de mídia (0.13.9). Fica de propósito: a prévia das formas desenha as ilhas de exemplo mesmo desligadas, e a opção
  desconhecida some ao regravar o perfil numa versão antiga (fixado em teste). Menu Iniciar com o vidro, a cor, a
  borda e os cantos da doca, por um componente dentro do Iniciar, com reserva pela cor de destaque do Windows (0.14.0).
  Notificações dos outros apps no notch e num painel no sino, com o balão do Windows calado (0.15.0), tela cheia que
  não cai ao clicar no outro monitor e balão do app do Claude calado (0.15.1), borda invisível da janela vizinha sem
  derrubar a tela cheia e aba Aparência sem esticar quando maximizada (0.15.2). Painel de controles na barra do topo
  (Bluetooth e aparelhos, modo avião, não perturbe que também cala o notch, luz noturna, brilho por tela) e microfone
  no painel de volume (0.16.0), acertos do brilho, do Bluetooth e do microfone (0.16.1), acertos das notificações e balões marcados no registro (0.16.2), perguntas do Claude no notch com as opções (0.17.0).
