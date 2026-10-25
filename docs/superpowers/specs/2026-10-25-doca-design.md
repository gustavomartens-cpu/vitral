# Doca: barra de baixo própria do Vitral

Data: 25/10/2026. Substitui a barra de tarefas do Windows por uma doca do Vitral, sem código dentro do Explorer.

## 1. Objetivo e motivo

Wi-Fi, volume e bateria aparecem duplicados: embaixo, no botão de configurações rápidas do Windows, e em cima, na ilha
de status do Vitral. O teste de 25/10 mostrou que no Windows 11 25H2 esse botão não pode ser escondido por
configuração: a opção por usuário não existe, e as políticas (`HideSCAVolume`/`Network`/`Power`), gravadas como
administrador, foram ignoradas mesmo com o Explorer reiniciado.

A solução escolhida é o Vitral **esconder a barra do Windows** e mostrar a sua: uma **doca flutuante central**
(mockup B, `.superpowers/brainstorm/1544-1792947813/content/barra-baixo-formato.html`). Os ícones de bandeja dos apps
passam para um painel na barra do topo.

## 2. Viabilidade (teste de 25/10, com o Gustavo na tela)

- **Bandeja:** com a biblioteca ManagedShell 0.0.374 (Apache-2.0, `TrayService`), um processo à parte virou o dono
  da bandeja e recebeu 14 ícones, encaminhando os cliques esquerdo e direito a cada app. O `TrayService` cria a
  própria `Shell_TrayWnd`, avisa os apps (`TaskbarCreated`) e repassa ao Explorer o que não trata, sem injetar
  nada (conferido na fonte).
- **Esconder a barra:** esconder a `Shell_TrayWnd` do Explorer e as `Shell_SecondaryTrayWnd` com `ShowWindow` por
  fora funcionou. O Iniciar (tecla Win) abriu embaixo normalmente. Em cerca de 20 s, com o Iniciar aberto quatro
  vezes, a barra não voltou sozinha. Mesmo assim, a vigilância continua necessária (ver §4).
- **Ao fechar o teste,** a barra e os ícones voltaram ao Windows.

## 3. Decisões

1. Formato B: uma ilha de vidro centralizada no monitor principal, 46 px de altura, 8 px da borda de baixo.
2. **Espaço reservado:** uma AppBar de 62 px embaixo do monitor principal. Janelas maximizadas terminam acima da doca.
3. **Só no monitor principal.** A barra do Windows some dos dois monitores. A doca mostra os apps de todos os
   monitores.
4. **Fixados:** importados uma vez dos atalhos fixados do Windows. Depois são gerenciados pela doca. Uma lista só,
   que não muda com o perfil.
5. **Bandeja:** um painel na barra do topo, aberto por uma setinha ^ na ilha de status.
6. **Primeira versão:** Iniciar, pesquisa, apps fixados e abertos, miniaturas e menu do botão direito.
7. **Faz parte do perfil:** seção `barraBaixo`, como a `barraTopo`.

## 4. Ligar, desligar e segurança

- **Perfil (esquema 4):** `"barraBaixo": { "visivel": true }`.
  - Ausente ou nulo = não mexer.
  - O estado aplicado fica no `config.json` e nos retratos, como a barra do topo (o mesmo padrão de `EstadoBarra`).
  - Esquemas 1 a 3 são lidos com `barraBaixo = null`.
- **Ligar:**
  1. Guardar o estado atual de "ocultar automaticamente" da barra do Windows.
  2. Ligar o ocultar automaticamente (`ABM_SETSTATE`), para o Windows liberar a área da barra.
  3. Esconder `Shell_TrayWnd` (a do Explorer, filtrada pelo processo, porque a do Vitral tem o mesmo nome) e todas
     as `Shell_SecondaryTrayWnd`.
  4. Registrar a reserva de 62 px e mostrar a doca.
- **Vigilância:**
  - A cada 250 ms, se uma barra do Windows estiver visível, esconder de novo.
  - Em `TaskbarCreated` (Explorer reiniciou) e mudança de monitores ou escala: esconder de novo, re-registrar a
    reserva e reposicionar a doca.
- **Desligar, sair do Vitral ou perfil que desliga:** mostrar as barras do Windows, voltar o ocultar automaticamente
  ao estado guardado e liberar a reserva.
- **Travamento:**
  - O `DispatcherUnhandledException` (rede que já existe) e o `AppDomain.UnhandledException` mostram as barras do
    Windows antes de qualquer coisa.
  - Ao abrir, se o estado aplicado for "desligada", o Vitral mostra as barras do Windows. Assim um travamento
    anterior nunca deixa o usuário sem barra depois de reabrir.
- **TranslucentTB:** com a doca ligada, não tem o que pintar. Não é tocado.

## 5. Doca

- **Vidro:** o mesmo acrílico das ilhas (`VidroIlha`). Fica no monitor principal, centralizada, e a largura
  acompanha o conteúdo.
- **Conteúdo, da esquerda para a direita:**
  - **Logo do Vitral:** abre o Iniciar (tecla Win via `SendInput`).
  - **Lupa:** abre a pesquisa (Win+S).
  - **Separador.**
  - **Botões dos apps:** um por app, com os fixados primeiro e na ordem da lista, depois os abertos não fixados na
    ordem em que abriram.
- **Agrupamento:** as janelas de um app ficam juntas pelo AppUserModelID. Sem AppUserModelID, pelo caminho do
  executável.
- **Janelas que entram:** visíveis, sem dono ou com `WS_EX_APPWINDOW`, sem `WS_EX_TOOLWINDOW` e não "cloaked" (o
  mesmo critério da ManagedShell `ApplicationWindow`). As janelas do próprio Vitral (ilhas, painéis, doca) ficam de
  fora.
- **Indicadores:**
  - App aberto: tracinho claro embaixo.
  - App com a janela ativa: tracinho âmbar mais largo, com brilho.
  - Janela pedindo atenção (piscando): tracinho âmbar pulsando.
- **Clique esquerdo:**
  - Sem janelas: abre o app (o atalho fixado).
  - Uma janela: se ativa, minimiza; se não, traz para a frente (e restaura, se estiver minimizada).
  - Várias janelas: abre o painel de miniaturas para escolher.
- **Passar o mouse (400 ms):** um painel de vidro acima do botão, com as miniaturas ao vivo (`DwmRegisterThumbnail`)
  e o título de cada janela. Clicar escolhe a janela, e um "×" fecha. Some ao tirar o mouse.
- **Botão direito:** menu com Fixar ou Desafixar, Abrir nova janela (o executável ou o atalho) e Fechar janela (ou
  "Fechar todas", com várias).
- **Arrastar:** reordena os fixados.
- **Clique do meio:** abre uma nova janela.
- **Tela cheia:** a doca some, como as ilhas do topo.

## 6. Fixados

- **Arquivo:** `%LOCALAPPDATA%\Vitral\fixados.json` com a lista de
  `{ "caminho": "...lnk ou .exe", "appId": "AUMID opcional", "nome": "..." }`.
- **Primeira vez:** importa os `.lnk` de
  `%APPDATA%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar`. O alvo e o AppUserModelID saem do
  `IShellLink` e da propriedade `System.AppUserModel.ID` do atalho.
- **Atalho inexistente:** fica na lista com ícone apagado e o clique avisa "Atalho não encontrado".

## 7. Bandeja no topo

- **Dono da bandeja:** a ManagedShell (`ShellManager` só com o `TrayService`, sem o `TasksService`) roda dentro do
  Vitral enquanto a doca está ligada. Com a doca desligada, a bandeja continua com o Windows.
- **Setinha ^:** a ilha de status ganha uma setinha que abre o painel novo "Ícones" (`Painel.Icones`, no mesmo
  coordenador dos painéis da v2). É uma grade de ícones, com o título como dica.
- **Cliques:** esquerdo e direito são repassados ao app (`IconMouseDown`/`IconMouseUp`).
- **Ícones de fora:** o de volume do Windows (o topo tem o próprio). Os outros ícones do sistema aparecem
  (Bluetooth, Remover hardware, Segurança do Windows).
- **Licença:** o aviso Apache-2.0 da ManagedShell vai para `THIRD-PARTY-NOTICES.md` na raiz.

## 8. Componentes

| Componente | Responsabilidade | Projeto |
|---|---|---|
| `BarraBaixoPerfil`, esquema 4, `EstadoDoca` (config e retrato) | Modelo e estado aplicado | Core |
| `IControleDoca` (`void Aplicar(BarraBaixoPerfil estado)`) | Ponto de ligar e desligar | Core (interface), App (real) |
| `GrupoApps`: agrupar janelas por app, ordem fixados + abertos, decidir a ação do clique | Lógica da doca | Ui (testada) |
| `DocaViewModel`, `BotaoAppViewModel`, `MiniaturasViewModel`, `PainelIconesViewModel` | Telas | Ui (testados com fontes falsas) |
| `IFonteJanelas` (janelas abertas, ativa, mudanças), `IFonteFixados`, `IFonteBandeja` | Dados | Ui (interfaces), App (Windows/ManagedShell) |
| `BarraDoWindows` | Esconder, mostrar, vigiar, ocultar automaticamente | App |
| `JanelaDoca`, `JanelaMiniaturas`, `PainelIcones` | Telas WPF | App |

## 9. Erros

- **Fonte com falha:** o mesmo contrato das fontes da barra do topo. Log em `barra.log` e o resto continua.
- **Bandeja que não inicia** (`NotificationArea.IsFailed`): a setinha some, e o aviso vai para o log e a bandeja do
  Vitral. A doca continua.
- **Esconder a barra falha** (nenhuma `Shell_TrayWnd` do Explorer encontrada): tenta de novo no próximo ciclo e
  registra no log uma vez.
- **App que não re-registra o ícone** depois que o Vitral assume a bandeja (caso conhecido: Riot Vanguard): fica
  sem ícone. É uma limitação documentada.

## 10. Testes

- **Automáticos:**
  - Esquema 4 em ida e volta e os esquemas antigos.
  - Estado da doca no config e no retrato: Testar, Voltar, Desfazer e Original.
  - Agrupamento por AUMID e por executável, e a ordem fixados + abertos.
  - Ação do clique por número de janelas e janela ativa.
  - Indicadores.
  - Importação dos fixados com atalho ausente.
  - Painel de ícones: lista sem o volume e cliques repassados.
- **Manuais, com o Gustavo:**
  - Ligar e desligar pelo perfil; Testar e Voltar.
  - Iniciar e pesquisa.
  - Abrir, alternar e minimizar apps.
  - Miniaturas e menu.
  - Fixar, desafixar e arrastar.
  - Bandeja pelo topo.
  - Reiniciar o Explorer.
  - Matar o Vitral e reabrir (a barra do Windows volta).
  - Tela cheia.

## 11. Ordem

1. `BarraDoWindows` e o estado da doca no perfil e no retrato, com a segurança do §4. A doca ainda é vazia.
2. Bandeja no topo (ManagedShell e painel Ícones).
3. Doca: apps abertos, agrupamento, clique e indicadores.
4. Fixados: importar, fixar, desafixar e arrastar.
5. Miniaturas e menu do botão direito.
6. Aba "Barra de baixo" no editor (ligada, desligada, não mexer).
7. Revisão final e instalador 0.5.0.

Um plano só.
