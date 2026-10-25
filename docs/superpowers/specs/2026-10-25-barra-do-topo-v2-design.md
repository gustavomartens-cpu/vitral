# Barra do topo v2: notch e painéis próprios

Data: 25/10/2026. Continua a barra do topo da spec `2026-10-24-identidade-e-barra-do-topo-design.md` (§3 a §8), já
instalada na versão 0.2.0. Mockup aprovado: `.superpowers/brainstorm/1544-1792947813/content/barra-v2-paineis.html`.

## 1. Objetivo

Hoje os cliques nas ilhas abrem os painéis do próprio Windows, que aparecem lá embaixo, junto da barra de tarefas. A v2
faz cada ilha abrir **um painel de vidro próprio que desce dela**, e a ilha do relógio vira um **notch** preso à borda
de cima da tela, de onde desce um calendário.

Fora do escopo: compromissos de agenda (Google/Outlook), lista de notificações (o Windows só deixa ler com identidade
de pacote) e a fase da barra de tarefas (o que sai de baixo para não duplicar). Ficam em `docs/ideias-pendentes.md`.

## 2. Decisões

1. Cinco painéis próprios: **calendário** (do notch), **Wi-Fi**, **volume**, **bateria e energia** (da ilha de
   status) e **mídia expandida** (da ilha de mídia).
2. Calendário só com o mês (sem agenda).
3. Cada painel é uma janela de vidro própria, com o mesmo acrílico das ilhas (abordagem 1). Descartadas: janela
   transparente única (em camadas, sem desfoque) e componentes do WinUI 3 (dependência pesada).
4. Entram as duas funções que dependem de interfaces não documentadas do Windows: troca do dispositivo de saída de
   áudio (`IPolicyConfig`) e modo de energia (`PowerSetActiveOverlayScheme`). Se uma falhar, o controle some do painel
   e o resto continua.
5. Os painéis não entram no perfil: existem sempre que a ilha correspondente está ligada. O esquema do perfil
   continua 2.

## 3. Forma

### 3.1 Notch

- A ilha do relógio passa a ficar presa à borda de cima do monitor principal: 34 px visíveis, só os cantos de baixo
  arredondados.
- Técnica: a janela começa **10 px acima da borda** (fora da tela) e tem 44 px de altura. Os cantos de cima, que o
  DWM arredonda junto com os de baixo, ficam fora da tela. O conteúdo fica centralizado nos 34 px visíveis. O
  acrílico é o mesmo das ilhas (decisão do vidro da v1).
- Mídia e status continuam como estão: ilhas soltas a 8 px do topo.

### 3.2 Painéis

- Vidro igual às ilhas: acrílico com `NoiteVidro`, `BordaVidro`, cantos do DWM.
- Descem a **6 px abaixo** da sua ilha (do notch, a 6 px da borda inferior dele).
- Alinhamento: o da mídia alinha pela esquerda da ilha; o calendário centralizado no notch; os da ilha de status
  alinham pela direita da ilha. Nunca saem do monitor (encostam a 12 px da borda).
- Larguras: calendário 248 px; Wi-Fi, volume e bateria 280 px; mídia 260 px.
- Ao abrir, o conteúdo desliza 8 px para baixo em 120 ms. Não há fade: a janela não é em camadas.

### 3.3 Conteúdo (do mockup)

- **Calendário:**
  - Título "outubro de 2026" com ‹ › para trocar de mês.
  - Grade de 6 semanas começando no domingo, com os dias de fora do mês apagados e hoje em âmbar.
  - Rodapé com a data por extenso ("domingo, 25 de outubro de 2026") e o link "Hoje", que volta ao mês atual.
- **Wi-Fi:**
  - Chave liga/desliga.
  - Lista de redes por perto, ordenada por sinal, com a conectada no topo destacada; cadeado nas protegidas.
  - Clique numa rede salva: conecta. Numa rede protegida nova: abre ali mesmo um campo de senha com "Conectar". Na
    conectada: "Desconectar".
  - Rodapé "Mais configurações de rede…", que abre `ms-settings:network-wifi`.
  - Redes corporativas (802.1X) abrem as configurações do Windows.
- **Volume:**
  - Número, controle deslizante 0–100 (aplica enquanto arrasta) e "Mudo".
  - Lista "Saída" com os dispositivos de reprodução ativos e o padrão marcado; clique troca o padrão.
- **Bateria e energia:**
  - Porcentagem grande e tempo restante ("3 h 20 min"), ou "Carregando" / "Conectado".
  - Barra âmbar.
  - "Modo de energia" com três pílulas: Economia, Equilibrado, Desempenho.
- **Mídia expandida:**
  - Capa grande (ou o ícone de música), título e "artista · app".
  - Barra de progresso com tempo decorrido e total, e ⏮ ⏸ ⏭.
  - Link "Abrir o app", que traz o app para a frente.

## 4. Comportamento

| Situação | Comportamento |
|---|---|
| Clique no notch | Abre/fecha o calendário |
| Clique na ilha de mídia (fora dos botões) | Abre/fecha a mídia expandida; ⏮ ⏸ ⏭ da ilha continuam agindo direto |
| Clique no ícone de rede | Abre/fecha o Wi-Fi |
| Clique no volume | Abre/fecha o volume (o mudo passa para dentro do painel); a roda continua ±2 |
| Clique na bateria | Abre/fecha bateria e energia |
| Clique em CPU/MEM | Continua abrindo o Gerenciador de Tarefas |
| Abrir outro painel | O anterior fecha: **um por vez** |
| Clique fora, Esc, clicar de novo na mesma ilha | Fecha o painel |
| App em tela cheia, barra desligada, Vitral saindo | Fecha qualquer painel aberto |
| Painel aberto | Recebe o foco (para Esc e o campo de senha); ao perder o foco, fecha |

A central de notificações (Win+N) e as configurações rápidas (Win+A) deixam de ser abertas pelos cliques. Continuam
acessíveis pelos atalhos do Windows.

## 5. Componentes

| Componente | Responsabilidade | Projeto |
|---|---|---|
| `CoordenadorPaineis` | Qual painel está aberto: abrir, alternar, fechar todos (tela cheia, desligar) | Ui (testado) |
| `CalendarioViewModel(IRelogioDoSistema)` | Mês exibido, grade 6×7, navegação, "Hoje", data por extenso em pt-BR | Ui (testado) |
| `PainelWifiViewModel(IFonteWifi)` | Lista ordenada, estados (conectando, precisa de senha, erro, sem permissão de localização), comandos | Ui (testado) |
| `PainelVolumeViewModel(IFonteVolume, IFonteDispositivosAudio)` | Controle deslizante, mudo, lista de saídas, troca do padrão; esconde a lista se a troca não estiver disponível | Ui (testado) |
| `PainelBateriaViewModel(IFonteBateria, IFonteModoEnergia)` | Textos (%, tempo, carregando), modo de energia; esconde o seletor se indisponível | Ui (testado) |
| `PainelMidiaViewModel(IFonteMidia, IRelogio)` | Capa, textos, progresso estimado a cada segundo enquanto toca | Ui (testado) |
| Fontes novas: `IFonteWifi`, `IFonteDispositivosAudio`, `IFonteModoEnergia`; `IFonteBateria` ganha o tempo restante; `IFonteMidia` ganha a linha do tempo (posição, duração, quando foi lida) | Mesmo contrato das fontes da v1: nunca lançam, eventos na thread da interface | Ui (interfaces), App (Windows) |
| `JanelaPainel` | Janela de vidro ativável, posicionamento sob a ilha, animação de entrada, fecha ao perder o foco ou com Esc | App |
| `JanelaIlha` (notch) | Variante "presa à borda" para o relógio | App |

APIs do Windows:
- **Wi-Fi:** `Windows.Devices.WiFi.WiFiAdapter`, com `RequestAccessAsync`, `ScanAsync`, `ConnectAsync` com
  `PasswordCredential` e `Disconnect`. Rádio por `Windows.Devices.Radios`. Rede atual por
  `NetworkInformation.GetInternetConnectionProfile`.
- **Dispositivos de áudio:** `IMMDeviceEnumerator.EnumAudioEndpoints` (saída, ativos), nome por
  `PKEY_Device_FriendlyName`, troca do padrão por `IPolicyConfig.SetDefaultEndpoint` nos três papéis (console,
  multimídia, comunicações).
- **Tempo de bateria:** `PowerManager.RemainingDischargeTime`.
- **Modo de energia:**
  - `PowerGetEffectiveOverlayScheme` / `PowerSetActiveOverlayScheme` (powrprof).
  - Economia: `961cc777-2547-4f9d-8174-7d86181b8a7a`.
  - Equilibrado: GUID vazio.
  - Desempenho: `ded574b5-45a0-4f42-8737-46345c09c238`.
- **Linha do tempo da mídia:** `GetTimelineProperties()` (`Position`, `EndTime`, `LastUpdatedTime`).

## 6. Erros

- **Fontes:** o contrato da v1 continua: falha vira null, "—" ou controle escondido, e o erro vai para
  `logs\barra.log`. Nada lança para fora de uma fonte.
- **Wi-Fi sem permissão de localização:** o painel mostra só a rede atual e a linha "Para ver redes por perto, permita
  a localização para apps da área de trabalho", com o botão "Abrir configurações" (`ms-settings:privacy-location`).
- **Wi-Fi sem adaptador** (desktop no cabo): o ícone de rede abre um painel com a conexão atual e "Mais configurações
  de rede…".
- **Falha ao conectar:** a mensagem do Windows aparece em uma linha no próprio painel (senha errada, rede fora de
  alcance).
- **`IPolicyConfig` ou `PowerSetActiveOverlayScheme` indisponível ou com erro:** a lista de saídas ou o seletor de
  modo some, e o erro vai para o log.

## 7. Testes

- **Automáticos (xUnit, fontes falsas):**
  - `CoordenadorPaineis`:
    - abrir A e depois B fecha A;
    - alternar o mesmo painel fecha;
    - fechar todos.
  - `CalendarioViewModel`:
    - outubro de 2026 começa na grade em 27/09;
    - 42 dias;
    - hoje marcado;
    - ‹ › trocam o mês e atravessam o ano;
    - "Hoje" volta;
    - data por extenso em pt-BR.
  - `PainelWifiViewModel`:
    - ordem (conectada primeiro, depois por sinal);
    - rede protegida nova pede senha;
    - erro de conexão vira mensagem;
    - sem permissão de localização mostra o aviso;
    - chave liga/desliga chega na fonte.
  - `PainelVolumeViewModel`:
    - o controle deslizante define o volume;
    - lista com o padrão marcado;
    - escolher troca o padrão;
    - troca indisponível esconde a lista.
  - `PainelBateriaViewModel`:
    - "3 h 20 min";
    - "Carregando";
    - tempo desconhecido não aparece;
    - modo atual marcado;
    - modo indisponível esconde o seletor.
  - `PainelMidiaViewModel`:
    - progresso e "1:16 / 3:20";
    - tique avança enquanto toca e para quando pausa;
    - sem duração esconde a barra.
- **Manuais, com o Gustavo na tela:**
  - o notch com desfoque e cantos certos;
  - cada painel abrindo sob a sua ilha;
  - um por vez;
  - fechar com clique fora e com Esc;
  - Wi-Fi: listar, conectar numa rede salva e desconectar;
  - volume: arrastar e trocar para o fone;
  - bateria: modo de energia (no notebook);
  - mídia: progresso andando.

## 8. Ordem

1. Notch: variante presa à borda, conferida na tela antes do resto.
2. `JanelaPainel` + `CoordenadorPaineis` + calendário (o primeiro painel completo valida posição, foco e fechamento).
3. Volume (dispositivos e `IPolicyConfig`).
4. Bateria e modo de energia.
5. Mídia expandida.
6. Wi-Fi (o mais trabalhoso: permissões, senha, estados).
7. Revisão final e instalador 0.3.0.

Um plano só, com essas tarefas.
