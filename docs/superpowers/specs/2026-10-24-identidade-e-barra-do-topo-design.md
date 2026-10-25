# Vitral: identidade "Vitral noturno" e barra do topo (fase 3)

Data: 2026-10-24 · Autor: Gustavo Martens (com Claude) · Estado: aguardando revisão
Spec mãe: `docs/superpowers/specs/2026-10-03-vitral-design.md` (§9, fase 3). Esboços: `docs/superpowers/mockups/identidade.html` (direção A) e `docs/superpowers/mockups/barra-topo-formato.html` (formato 2).

## 1. Objetivo

1. Dar personalidade ao Vitral: a janela, a contagem do "Testar", a bandeja e a barra do topo ganham a identidade **"Vitral noturno"**, que junta vidro com um escuro sofisticado.
2. Construir a **barra do topo** como parte do Vitral: três ilhas de vidro flutuantes no monitor principal, controladas por perfil.

### Critérios de sucesso

1. A janela do Vitral fica reconhecível como "Vitral noturno": vidro azul-noite, âmbar nos pontos de ação, losango de vitral como marca.
2. Com a barra ligada no perfil, as três ilhas aparecem no topo do monitor principal e janelas maximizadas começam abaixo delas.
3. A barra mostra relógio e data; mídia tocando com controles; volume, rede e bateria (no notebook); e CPU e memória.
4. Testar, Aplicar, Desfazer, Voltar ao original e a troca pela bandeja também valem para a barra.
5. A barra some em tela cheia, volta depois e se recupera sozinha quando o Explorer reinicia.
6. Uma falha num dado da barra nunca derruba a barra nem o app.

### Fora do escopo

- Barra em outros monitores (decidido: só no principal).
- Atalhos do Vitral na barra (troca de perfil continua na bandeja).
- Bandeja do sistema (ícones da área de notificação) e lista de tarefas próprias.
- Calendário ou agenda própria (o clique no relógio abre o do Windows).
- Personalizar cores e posição das ilhas pela interface (fica para depois; a primeira versão tem o visual fixo da identidade).

## 2. Identidade "Vitral noturno"

### 2.1 Tokens

| Token | Valor | Uso |
|---|---|---|
| `Noite` | `#0A0F20` | tinta do vidro (janela e ilhas), opacidade ~72% |
| `NoiteLateral` | `#060A16` a ~35% | barra lateral da janela |
| `Âmbar` | `#F2B544` | ação: botão primário, aba ativa, perfil selecionado, progresso |
| `ÂmbarGradiente` | `#F7C766` → `#E39A2B` (135°) | preenchimento do botão primário |
| `Brilho` | âmbar a 45% | sombra ou brilho dos elementos âmbar |
| `Azul` | `#4C7DF0` | apoio: segunda cor do logo, links, foco |
| `Texto` | `#EDEFF7` | texto principal; secundário a 65% de opacidade |
| `Linha` | branco a 8% | divisórias e bordas internas |
| `Borda` | branco a 10% | borda externa de vidro |
| `Especular` | branco a 14% | filete de 1 px no alto dos cartões e das ilhas |
| `Raio` | 16 px (janela e cartões), 10 px (botões), pílula (seletores e ilhas) | cantos |
| Fonte | Segoe UI Variable Display (títulos 22 px semibold) e Segoe UI Variable Text (corpo) | do Windows 11, sem fonte extra |

### 2.2 Regras

- **Sempre escuro.** A identidade é noturna; a janela do Vitral não segue o modo claro do Windows (`ThemeMode="Dark"`).
- **Fundo da janela:** acrílico do sistema (`DWMSBT_TRANSIENTWINDOW`) com a tinta `Noite`. Se o acrílico não estiver disponível (transparência desligada ou economia de bateria), o fundo é `Noite` sólido.
- **Âmbar só onde há ação.** Nada decorativo em âmbar além do logo.
- **Componentes:**
  - perfil na lista com um losango de 9 px na cor de destaque daquele perfil (azul-noite se o perfil não define cor);
  - selecionado com uma barra âmbar de 3 px e brilho à esquerda;
  - o perfil ativo com a etiqueta "ativo" em âmbar;
  - seletores de três estados em pílula (Não mexer · Sim · Não), substituindo as caixas de seleção;
  - cartões de vidro com o filete `Especular`.
- **Logo:** losango rotacionado com quatro gomos, âmbar e azul alternados. É usado no título da janela, no ícone do app e da bandeja (o `vitral.ico` é refeito nessas cores) e no instalador.
- **Janela da contagem:** mesmo vidro, título "Manter este visual?", barra de progresso âmbar com brilho, botões "Voltar" e "Manter" (este primário).
- **Pendências da revisão 1d que entram aqui:** botão **Descartar** no rodapé (recarrega o perfil do disco) e marca do perfil **ativo** na lista.

## 3. Barra do topo

### 3.1 Forma

- **Três ilhas** no monitor principal, 30 px de altura, com 8 px de margem do topo. Pílulas de vidro (`Noite` ~72%, `Borda`, `Especular`).
  - **Mídia** (à esquerda, a 12 px da borda): capa ou ícone do app, título truncado com "…", e ⏮ ⏸ ⏭ (o de pausar/tocar em âmbar). **Some** quando nada toca.
  - **Relógio** (centro): `HH:mm` em semibold e a data curta (`sex, 24 out`), em pt-BR.
  - **Status** (à direita, a 12 px da borda): CPU e MEM (mini barras âmbar com a porcentagem), separador, ícone de rede, volume com número, e bateria com porcentagem (só se houver bateria).
- **Reserva:** uma AppBar invisível de **46 px** no topo do monitor principal (`SHAppBarMessage`: `ABM_NEW`, `ABM_QUERYPOS`, `ABM_SETPOS`, `ABE_TOP`). As ilhas são janelas separadas, sobre essa faixa.

### 3.2 Perfil e retrato

- O esquema do perfil passa para **2**:

  ```json
  "barraTopo": { "visivel": true, "ilhas": { "midia": true, "relogio": true, "status": true, "desempenho": true } }
  ```

  - `barraTopo` ausente ou `null` significa "não mexer".
  - `ilhas` ausente significa todas ligadas.
  - `desempenho` controla CPU e MEM dentro da ilha de status.
- **Migração:** perfis no esquema 1 são lidos normalmente, com `barraTopo = null`. Ao salvar, são gravados como esquema 2. Pacotes `.vitral` de esquema 1 continuam importáveis.
- **Estado aplicado:** o estado atual da barra fica em `config.json` (`barraTopo`, mesmo formato). O `Aplicador` aplica a seção do perfil e a guarda ali.
- **Retrato:** o retrato ganha `barraTopo` (o estado antes da mudança). Reverter, desfazer, voltar ao original e cancelar teste restauram a barra junto com o registro. Retratos antigos sem `barraTopo` restauram a barra como "não mexer".
- **No login,** o perfil ativo reaplicado também liga a barra.

### 3.3 Componentes

| Componente | Responsabilidade | Projeto |
|---|---|---|
| `BarraTopoPerfil` (record) + leitura e escrita no `PerfilJson` (esquema 2 e migração) | Modelo e serialização | Core |
| `EstadoBarra` em `ConfigApp` e no `Retrato` | Estado aplicado e reversível | Core |
| `IControleBarra` (`void Aplicar(BarraTopoPerfil? estado)`) | Ponto em que o `Aplicador` liga, desliga ou ajusta a barra; o falso guarda as chamadas para os testes | Core (interface), App (real) |
| Fontes: `IFonteMidia`, `IFonteVolume`, `IFonteRede`, `IFonteBateria`, `IFonteDesempenho`, `IRelogioDoSistema` | Dados com evento de mudança, ou leitura periódica de 1 s para desempenho e relógio; cada uma devolve `null` quando indisponível | Ui (interfaces), App (Windows) |
| `IlhaMidiaViewModel`, `IlhaRelogioViewModel`, `IlhaStatusViewModel` | O que mostrar, quando mostrar e os comandos (tocar/pausar, mudo, volume ±) | Ui (testados) |
| `ReservaTopo` | Registro, reposicionamento e liberação da AppBar; detecção de tela cheia (`ABN_FULLSCREENAPP`); re-registro em `TaskbarCreated` | App |
| `JanelaIlha` (×3) | Janelas WPF sem borda, sempre no topo, fora da barra de tarefas, com vidro | App |

Fontes reais, todas APIs do Windows, sem pacote novo além do que já existe:
- **Mídia:** `GlobalSystemMediaTransportControlsSessionManager` (WinRT).
- **Volume:** Core Audio (`IAudioEndpointVolume`) por COM interop.
- **Rede:** `NetworkInformation` (WinRT).
- **Bateria:** `Windows.System.Power.PowerManager`.
- **CPU:** `GetSystemTimes`. **Memória:** `GlobalMemoryStatusEx`.
- O projeto App passa a mirar `net10.0-windows10.0.19041.0`, por causa das APIs WinRT.

### 3.4 Vidro (primeira tarefa da fase)

A barra nunca está ativa. O backdrop do sistema (Mica e acrílico) fica sólido em janela inativa. A primeira tarefa da fase é um **teste comparativo** numa ilha de teste, com a janela inativa e com a economia de bateria ligada e desligada. Ele compara três técnicas:
1. `SetWindowCompositionAttribute` com `ACCENT_ENABLE_BLURBEHIND` e tinta (o que o YASB usa no Windows 11);
2. `ACCENT_ENABLE_ACRYLICBLURBEHIND`;
3. `DWMWA_USE_HOSTBACKDROPBRUSH` com pincel de composição.

A que mantiver o desfoque com a janela inativa vai para as ilhas e é registrada no plano. Se nenhuma mantiver, o fallback combinado é `Noite` a 85% sem desfoque, com aprovação do Gustavo antes de seguir.

## 4. Comportamento

| Situação | Comportamento |
|---|---|
| Clique em ⏮ ⏸ ⏭ | Controla a sessão de mídia atual |
| Clique no título da mídia | Traz para a frente a janela do app que está tocando (quando houver) |
| Clique no relógio | Abre a central de notificações e o calendário do Windows (atalho Win+N) |
| Roda do mouse no volume | ±2 pontos por passo; clique liga e desliga o mudo |
| Clique em rede ou bateria | Abre as configurações rápidas do Windows (Win+A) |
| Clique em CPU ou MEM | Abre o Gerenciador de Tarefas |
| App em tela cheia no monitor principal | As ilhas somem; voltam ao sair da tela cheia |
| Explorer reinicia | Re-registra a reserva e reposiciona as ilhas (mensagem `TaskbarCreated`) |
| Mudança de monitores, resolução ou escala | Reposiciona no monitor principal atual (`WM_DISPLAYCHANGE`, mudança de DPI) |
| Suspender e retomar | Atualiza todos os dados ao retomar |
| Perfil ou app desliga a barra | Fecha as ilhas e libera a reserva (`ABM_REMOVE`) |
| Vitral fecha ou cai | O Windows libera a reserva sozinho; as ilhas fecham junto com o processo |

## 5. Erros

- **Fonte indisponível ou com falha:** aquela informação mostra "—", as outras continuam e o erro vai para `%LOCALAPPDATA%\Vitral\logs`. Nunca há exceção para fora da fonte.
- **Falha ao registrar a reserva:** as ilhas aparecem mesmo assim (sem reservar o espaço) e um aviso vai para a bandeja.
- **Exceção inesperada na interface:** passa pela rede de segurança que já existe (`DispatcherUnhandledException`): aviso e log, e o app continua.
- **Mídia:** sem sessão ativa, a ilha some. Sem permissão (raro), também some e registra no log.

## 6. Testes

- **Automáticos (xUnit):**
  - `PerfilJson`: esquema 2 em ida e volta; esquema 1 lido com `barraTopo = null`; `ilhas` ausente significa todas ligadas.
  - `Aplicador` e `ServicoVitral` com `IControleBarra` falso: aplicar, testar, cancelar, desfazer e original levam o estado da barra para frente e para trás. Retrato antigo sem `barraTopo` resulta em "não mexer".
  - ViewModels das ilhas com fontes falsas:
    - a mídia some sem sessão;
    - o título é truncado;
    - a bateria só aparece com bateria;
    - fonte que devolve `null` resulta em "—";
    - roda do volume ±2, sem passar de 0 a 100;
    - relógio e data em pt-BR.
- **Manuais, com o Gustavo na tela:**
  - teste de vidro;
  - maximizar uma janela (começa abaixo das ilhas);
  - vídeo em tela cheia (somem e voltam);
  - reiniciar o Explorer (voltam);
  - Testar um perfil com a barra ligada e outro desligada;
  - mídia no Spotify e no navegador;
  - volume pela roda;
  - clique no relógio e no Wi-Fi.

## 7. Ordem

1. **Identidade no app:** tokens, janela, contagem, logo novo, Descartar, marca do ativo.
2. **Barra do topo:**
   1. teste de vidro;
   2. reserva e ilhas vazias;
   3. fontes e ilhas;
   4. integração com perfil e retrato;
   5. comportamento (tela cheia, Explorer, monitores);
   6. instalador 0.2.0.

Cada um vira um plano separado.

## 8. Decisões registradas

1. Direção visual A, "Vitral noturno"; sempre escuro.
2. Conteúdo da barra: essenciais, mídia e desempenho (sem atalhos do Vitral).
3. Só no monitor principal.
4. Três ilhas flutuantes.
5. Barra faz parte do perfil (esquema 2).
6. Barra própria, enxuta, dentro do processo do Vitral (sem ManagedShell, sem processo separado).
