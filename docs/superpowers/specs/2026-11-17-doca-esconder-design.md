# Doca escondendo automaticamente

Conversa de desenho com o Gustavo em 17/11/2026.

## 1. Por quê

O Gustavo quer a tela inteira para os apps: com a opção ligada, a doca some e deixa de reservar espaço, as janelas
maximizadas (de qualquer app) vão até embaixo, e a doca volta por cima delas quando o mouse encosta na borda de baixo —
como o "ocultar automaticamente a barra de tarefas" do Windows.

## 2. O que fica (decisões do Gustavo)

- **Some sempre** que o mouse sai dela, mesmo com a área de trabalho vazia (não é "só quando uma janela encosta").
- **Aparece** com o mouse ~0,1 s na faixa de ativação; **some** ~0,5 s depois que o mouse sai dela.
- **Faixa de ativação:** os 2 px de baixo do monitor principal, na largura toda dele.
- **Opção por perfil**, na aba da doca, no padrão das outras: "Esconder automaticamente" com Não mexer / Ligado /
  Desligado.

## 3. Comportamento (chamadas minhas, para o Gustavo conferir)

- **Reserva:** ligado, a faixa da doca sai do registro da AppBar (`ReservaFaixa.Liberar`) e as janelas maximizadas
  crescem; desligado, registra de novo. A barra do topo não muda.
- **Mouse sobre a doca:** a doca fica enquanto o mouse estiver no retângulo dela estendido até a borda de baixo (o vão de
  8 px da flutuante conta como "dentro").
- **Não esconde no meio:** com o menu do botão direito, as miniaturas ou um arraste em andamento (de botão ou de
  arquivo), a doca não começa a sumir; o tempo de 0,5 s conta a partir do fim disso.
- **Arrastar arquivo (0.20.0):** encostar o arquivo arrastado na borda faz a doca aparecer, como com o mouse (a posição
  do cursor é lida durante o arraste).
- **Tela cheia:** continua escondendo como hoje e ganha da borda (com um app em tela cheia, encostar não mostra a doca).
- **Animação:** a doca desliza para baixo até sair da tela (~150 ms) e sobe ao aparecer; com "Efeitos de animação" do
  Windows desligado (`Movimento.Ligado`), some e aparece direto. Escondida, a janela fica oculta (`Hide`), sem área
  invisível roubando cliques.
- **Vários monitores:** a doca só existe no principal; nos outros nada muda. Se houver um monitor abaixo do principal,
  a borda de baixo do principal não é borda física — o mouse passa direto e a ativação depende de parar ali (limite
  aceito; vai para as ideias pendentes se incomodar).
- **Ligar a opção** com a doca visível: ela some depois de 0,5 s sem mouse nela. **Desligar:** aparece e volta a
  reservar.

## 4. Abordagens

1. **Escolhida — soltar a reserva e conferir o cursor por timer.** Timer de ~50 ms lê `GetCursorPos`; uma máquina de
   estados pura em `Vitral.Ui` (escondida → aparecendo → visível → sumindo) recebe o tempo, a posição do cursor, as
   travas (menu, miniaturas, arraste) e a tela cheia, e diz o que fazer. Testável sem WPF; funciona durante arraste OLE.
2. Gancho global de mouse (`WH_MOUSE_LL`): mais rápido, mas a thread da interface vira gargalo do mouse do Windows
   inteiro se travar (já travou depois da suspensão, 29/10). Descartada.
3. Janela invisível de 2 px na borda: rouba cliques do rodapé dos apps maximizados. Descartada.
4. Ocultação nativa da AppBar (`ABM_SETAUTOHIDEBAR`): uma por borda, e a de baixo é da barra do Windows (que a doca
   mantém com "ocultar automaticamente" ligado). Descartada.

## 5. Modelo

- `BarraBaixoPerfil(bool Visivel, bool? EsconderAutomatico = null)`: nulo é "não mexer" (mantém o aplicado). O
  `EstadoDoca` grava o valor resolvido no `config.json` e o repassa ao `IControleDoca`.
- O seletor "Esconder automaticamente" só vale com a doca "Ligada" no perfil (desabilitado nos outros casos).
- Retratos ("Voltar") levam o valor junto, sem trabalho extra (é o mesmo registro).

## 6. Fora do escopo

- Mostrar a doca ao abrir o Menu Iniciar ou por atalho de teclado.
- Doca nos outros monitores.
- Tempos configuráveis.
