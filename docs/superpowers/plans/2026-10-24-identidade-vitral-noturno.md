# Identidade "Vitral noturno" no app (plano de implementação)

> **Para agentes:** sub-skill obrigatória: use superpowers:subagent-driven-development (recomendado) ou superpowers:executing-plans para executar este plano tarefa por tarefa. Os passos usam checkbox (`- [ ]`) para acompanhamento.

**Objetivo:** aplicar a identidade "Vitral noturno" na janela, na contagem do "Testar", no ícone e na bandeja. Inclui o botão **Descartar** e a marca do perfil **ativo** na lista.

**Arquitetura:** os tokens e estilos ficam num dicionário de recursos do app (`Tema/`), sem tocar no núcleo. O que tem lógica fica no `Vitral.Ui` e é testado: itens da lista com cor e ativo, opções dos seletores em pílula e o comando Descartar. O vidro da janela é o acrílico do sistema (DWM), com fallback sólido.

**Stack:** WPF .NET 10 (ThemeMode Dark), CommunityToolkit.Mvvm, P/Invoke DWM (`DwmSetWindowAttribute`, `DwmExtendFrameIntoClientArea`).

**Spec:** `docs/superpowers/specs/2026-10-24-identidade-e-barra-do-topo-design.md` (§2). Esboço: `docs/superpowers/mockups/identidade.html` (direção A).

## Restrições globais

- Tokens exatos (spec §2.1):
  - `Noite #0A0F20` (vidro com ~72% de opacidade, alfa `B8`);
  - `NoiteLateral #060A16` a 35% (alfa `59`);
  - `Âmbar #F2B544`;
  - gradiente `#F7C766 → #E39A2B` a 135°;
  - `Azul #4C7DF0`;
  - `Texto #EDEFF7` (secundário a 65%);
  - `Linha` = branco a 8% (`#14FFFFFF`), `Borda` = branco a 10% (`#1AFFFFFF`), `Especular` = branco a 14% (`#24FFFFFF`);
  - raios: 16 px (janela e cartões), 10 px (botões), pílula (seletores).
- Fonte: Segoe UI Variable Display nos títulos (22 px, SemiBold) e Segoe UI Variable Text no corpo. Nenhuma fonte instalada.
- `ThemeMode="Dark"` fixo.
- Âmbar só em ação: botão primário, aba ativa, barra do selecionado, etiqueta "ativo", progresso e foco do seletor.
- O foco de teclado tem de ficar visível em todos os controles (contorno azul `#4C7DF0`, 2 px).
- Nenhuma mudança em `Vitral.Core`.

## Foco de revisão

1. **Transparência do Windows desligada ou economia de bateria:** a janela usa `Noite` sólido e o texto continua legível. Verificação manual na Tarefa 4.
2. **Papel de parede claro atrás da janela:** o texto continua legível, porque a tinta de 72% garante o contraste. Verificação manual na Tarefa 4.
3. **Marca de "ativo" desatualizada** depois de Manter, Aplicar, Desfazer, Original ou troca pela bandeja: a lista reflete o `PerfilAtivo` atual. Teste na Tarefa 1.
4. **Descartar sem perfil selecionado** ou com perfil que sumiu do disco: aviso, sem exceção. Teste na Tarefa 1.
5. **Navegação só por teclado** (Tab, setas nos seletores, Enter nos botões): todo controle recebe foco visível. Verificação manual na Tarefa 4.

---

### Tarefa 1: lógica da lista e do Descartar (Vitral.Ui)

**Arquivos:**
- Criar: `src/Vitral.Ui/ItemPerfil.cs`, `src/Vitral.Ui/Opcoes.cs`
- Modificar: `src/Vitral.Ui/PrincipalViewModel.cs`
- Teste: `tests/Vitral.Ui.Tests/IdentidadeTests.cs`

**Interfaces:**
- Produz:
  - `sealed record ItemPerfil(string Nome, string CorGema, bool Ativo)`. `CorGema` é a `corDestaque` do perfil ou, sem ela, `#21375E`.
  - `PrincipalViewModel.ItensPerfis` (`ObservableCollection<ItemPerfil>`), mantida em sincronia com `Perfis` e com `PerfilAtivo`. A sincronia nunca usa Reset: troca um item com `[i] = novo` e insere ou remove pontualmente.
  - `RelayCommand Descartar`: recarrega o editor do disco. Sem seleção, ou se o perfil sumiu, avisa pelo `IDialogos`.
  - `sealed record Opcao<T>(string Rotulo, T Valor)` e as listas estáticas em `Opcoes`:
    - `Modos` = Não mexer · Claro · Escuro;
    - `Escolhas` = Não mexer · Sim · Não;
    - `Ligada` = Não mexer · Ligada · Desligada;
    - `Alinhamentos` = Não mexer · Centro · Esquerda.

- [ ] **Passo 1: testes que falham:**
  - um perfil com `corDestaque #E81123` gera `ItemPerfil("X", "#E81123", false)`; sem cor, gera `"#21375E"`;
  - **foco 3:** depois de `Testar` e `Manter`, o item do perfil fica com `Ativo == true` e nenhum outro; depois de `Aplicar` outro perfil, a marca muda de item;
  - a sincronia não emite `Reset` (como no teste da 1d);
  - `Descartar` depois de editar `Transparencia` volta ao valor salvo;
  - **foco 4:** `Descartar` sem seleção gera 1 aviso; com o perfil apagado do disco, também gera 1 aviso, sem exceção;
  - `Opcoes.Modos` tem 3 itens na ordem Não mexer, Claro, Escuro, com os valores do enum correspondente.
- [ ] **Passo 2:** `dotnet test tests/Vitral.Ui.Tests` → FAIL.
- [ ] **Passo 3:** implementar.
- [ ] **Passo 4:** `dotnet test` → as duas suítes PASS.
- [ ] **Passo 5:** commit `feat(ui): itens de perfil com cor e ativo, opções e Descartar`.

### Tarefa 2: tokens e estilos

**Arquivos:**
- Criar: `src/Vitral.App/Tema/Tokens.xaml` (cores, pincéis, raios e fontes) e `src/Vitral.App/Tema/Estilos.xaml`, com os estilos:
  - `BotaoPrimario` (gradiente âmbar, texto `#1A1206`, brilho âmbar);
  - `BotaoVidro` (fundo branco a 4%, `Borda`);
  - `AbaVitral` (sublinhado âmbar de 2 px com brilho na aba ativa; abas desabilitadas a 40%);
  - `ItemListaPerfil` (gema rotacionada 45°, barra âmbar de 3 px no selecionado, etiqueta "ativo");
  - `CartaoVidro` (`Border` com raio 16 e filete `Especular` no topo);
  - `SeletorPilula` (`ListBox` horizontal com `RadioButton` visual; item selecionado em âmbar);
  - `FocoVitral` (contorno azul de 2 px).
- Modificar: `src/Vitral.App/App.xaml` (mescla os dicionários e passa a `ThemeMode="Dark"`).

**Interfaces:**
- Consome: `ItemPerfil` e `Opcao<T>` da Tarefa 1.
- Produz: as chaves de recurso acima, usadas nas Tarefas 3 e 4.

- [ ] **Passo 1:** escrever os dois dicionários com os valores das restrições globais.
- [ ] **Passo 2:** `dotnet build -c Release` → `0 Erro(s)` e `0 Aviso(s)`.
- [ ] **Passo 3:** commit `feat(app): tokens e estilos do Vitral noturno`.

### Tarefa 3: logo e ícone

**Arquivos:**
- Criar: `tools/gerar-icone.ps1`, que gera `src/Vitral.App/Recursos/vitral.ico` (16, 24, 32, 48 e 256 px). O desenho é um losango rotacionado com 4 gomos, âmbar `#F2B544` e azul `#4C7DF0` alternados, um filete escuro `#0A0F20` entre os gomos e fundo transparente (sem o quadrado azul atual).
- Criar: `src/Vitral.App/Tema/Logo.xaml`, o mesmo desenho em XAML vetorial (`DrawingImage`), para o título da janela.

- [ ] **Passo 1:** escrever o script e gerar o ícone.
- [ ] **Passo 2: verificar:** exportar a versão de 256 px para PNG no scratchpad e olhar. Esperado: 4 gomos alternados, nítido, com fundo transparente.
- [ ] **Passo 3:** commit `feat(app): logo de vitral âmbar e azul`.

### Tarefa 4: janela, contagem e vidro

**Arquivos:**
- Criar: `src/Vitral.App/Vidro.cs`, com `static void Aplicar(Window janela)`:
  - se `HKCU\...\Themes\Personalize\EnableTransparency == 1`, usa `DwmExtendFrameIntoClientArea(-1)` com `DWMWA_SYSTEMBACKDROP_TYPE = DWMSBT_TRANSIENTWINDOW (3)`, `DWMWA_USE_IMMERSIVE_DARK_MODE = 1` e fundo da janela com a tinta `Noite` a `B8`;
  - senão, usa o fundo `Noite` sólido;
  - registra em `SourceInitialized` e reaplica quando a configuração de transparência mudar (`SystemEvents.UserPreferenceChanged`).
- Modificar:
  - `src/Vitral.App/JanelaPrincipal.xaml`: layout do esboço A:
    - barra lateral `NoiteLateral` com marca e "Vitral", a lista de perfis (`ItensPerfis`, `ItemListaPerfil`, `SelectedValuePath="Nome"`, `SelectedValue={Binding Selecionado}`) e "+ Novo perfil";
    - título de 22 px e abas `AbaVitral`;
    - opções em `CartaoVidro` com `SeletorPilula`;
    - rodapé com mensagem, **Descartar**, Salvar, Testar (20 s) e Aplicar (`BotaoPrimario`);
    - Importar, Exportar, Desfazer e Voltar ao original como `BotaoVidro` na lateral.
  - `src/Vitral.App/JanelaContagem.xaml`: vidro, título com a fonte de títulos, progresso âmbar com brilho, Voltar (`BotaoVidro`) e Manter (`BotaoPrimario`).
  - `src/Vitral.App/JanelaPrincipal.xaml.cs` e `JanelaContagem.xaml.cs`: chamar `Vidro.Aplicar`.

- [ ] **Passo 1:** implementar. `dotnet build -c Release` → `0 Erro(s)`. Rodar `dotnet test`: as duas suítes passam.
- [ ] **Passo 2: verificação manual com o Gustavo:**
  - abrir o app e comparar com o esboço A;
  - selecionar perfis (barra âmbar), conferir a etiqueta "ativo", editar uma opção e clicar **Descartar** (volta);
  - **Testar**: a janelinha com o vidro novo;
  - **foco 1:** desligar "Efeitos de transparência" nas Configurações: o fundo fica sólido e legível; religar;
  - **foco 2:** com um papel de parede claro atrás, o texto continua legível;
  - **foco 5:** percorrer a janela só com Tab, setas e Enter: o foco fica visível em tudo.
- [ ] **Passo 3:** ajustes que o Gustavo pedir na verificação. Cada um vira um commit pequeno e é registrado no ledger.
- [ ] **Passo 4:** commit `feat(app): janela e contagem no estilo Vitral noturno`.
- [ ] **Passo 5:** gerar o instalador `0.1.2` (`pwsh build.ps1 -Versao 0.1.2`) e, **com autorização**, atualizar o app instalado.
