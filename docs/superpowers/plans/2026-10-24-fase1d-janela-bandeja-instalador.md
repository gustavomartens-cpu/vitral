# Fase 1d: janela, bandeja e instalador (plano de implementação)

> **Para agentes:** sub-skill obrigatória: use superpowers:subagent-driven-development (recomendado) ou superpowers:executing-plans para executar este plano tarefa por tarefa. Os passos usam checkbox (`- [ ]`) para acompanhamento.

**Objetivo:** transformar o núcleo da fase 1a num app instalável. O app tem uma janela para criar, editar, testar e aplicar perfis de **ajustes nativos**, um ícone na bandeja para trocar de perfil, inicia com o Windows reaplicando o perfil ativo e tem um `Setup.exe` por usuário para levar ao notebook.

**Arquitetura:** três camadas, cada uma testável no seu nível.
- `Vitral.Core` (fase 1a) ganha **configuração**, **inicialização com o Windows** e um **serviço de alto nível** (`ServicoVitral`), que a janela e a bandeja usam.
- `Vitral.Ui` é uma biblioteca **sem WPF**, com os ViewModels (CommunityToolkit.Mvvm). É testada com xUnit e com relógio e diálogos falsos.
- `Vitral.App` é o executável WPF, só com XAML e a ligação com o Windows (diálogos, bandeja, instância única). Ele é verificado à mão.

**Stack:** .NET 10 WPF com tema Fluent nativo (`ThemeMode="System"`) e Mica; CommunityToolkit.Mvvm (MIT); H.NotifyIcon.Wpf (MIT) para a bandeja; Velopack (MIT) para o instalador.

**Spec:** `docs/superpowers/specs/2026-10-03-vitral-design.md` (§4.3 testar e reverter, §5 interface, §6 erros, §8 build e instalação). Núcleo: `docs/superpowers/plans/2026-10-24-fase1a-nucleo-e-nativo.md`.

**Escopo desta fase:** só os ajustes nativos. As abas Barra, Iniciar, Central, Explorer, Barra do topo e Janelas aparecem **desabilitadas**, com a etiqueta "em breve". A barra transparente continua com o TranslucentTB do Gustavo; a fase 1d não o detecta, não o fecha nem o configura.

**Ainda fora, de propósito:** posição e tamanho novos da barra (KB5124008; precisa detectar a liberação gradual), logs com rotação de 7 dias, atualização automática pelo Velopack (a v1 atualiza rodando o Setup novo) e os 14 pontos menores da revisão da 1a (ledger).

## Restrições globais

- Ferramentas e caches em `G:\dev` (`. tools/env.ps1`). Sem admin. Pacotes NuGet só estes: `CommunityToolkit.Mvvm`, `H.NotifyIcon.Wpf`, `Velopack`, mais a ferramenta `vpk` instalada em `G:\dev\dotnet-tools`.
- Escrita só em HKCU. A inicialização com o Windows usa `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, valor `Vitral`, texto `"<caminho do exe>" --inicializacao`.
- Dados em `%LOCALAPPDATA%\Vitral` (spec §4.1). Novo: `config.json` com `{ "perfilAtivo": string|null, "iniciarComWindows": bool, "testeEmAndamento": { "perfil": string, "retrato": string } | null }`.
- Prazo do "Testar" na janela: **20 s**. Prazo da troca pela bandeja: **12 s** (spec §5). Sem resposta, volta ao anterior.
- Toda mudança passa pelo `Aplicador` da 1a, ou seja, sempre com retrato antes. Nunca relatar sucesso parcial como total (spec §6): itens não aplicados aparecem na janela ou na notificação da bandeja.
- Uma instância só. Abrir de novo traz a janela existente para a frente.
- Textos da interface em português do Brasil.

## Foco de revisão

1. **App fechado ou morto durante a contagem do "Testar":** ao abrir de novo, ele precisa **voltar ao anterior** (marca `testeEmAndamento` no `config.json`). Teste na Tarefa 2.
2. **"Não mexer" × "desligado" no editor:** um campo que o perfil não define precisa continuar `null` ao salvar, e não virar `false`. Teste na Tarefa 3.
3. **Perfil ativo apagado ou inválido no login:** o app inicia na bandeja sem aplicar nada e avisa. Não pode quebrar. Teste na Tarefa 2.
4. **Importar perfil com o mesmo nome:** a janela pergunta se quer substituir (usa o `--substituir` da 1a). Teste na Tarefa 3.
5. **Duas instâncias ao mesmo tempo** (login mais clique no atalho): só uma fica; a outra traz a janela para a frente e sai. Verificação manual na Tarefa 5.

---

### Tarefa 1: configuração e inicialização com o Windows (núcleo)

**Arquivos:**
- Criar: `src/Vitral.Core/Configuracao/ConfigApp.cs`, `src/Vitral.Core/Configuracao/ServicoConfig.cs`, `src/Vitral.Core/Configuracao/Inicializacao.cs`
- Teste: `tests/Vitral.Core.Tests/ConfigTests.cs`

**Interfaces:**
- Produz:
  - `sealed record TesteEmAndamento(string Perfil, string Retrato)` (`Retrato` = nome do arquivo do retrato salvo antes do teste)
  - `sealed record ConfigApp(string? PerfilAtivo, bool IniciarComWindows, TesteEmAndamento? TesteEmAndamento)`, com `static ConfigApp Padrao => new(null, true, null)`
  - `sealed class ServicoConfig(string arquivo) { ConfigApp Ler(); void Gravar(ConfigApp c); }`. Arquivo ausente ou JSON inválido → `Padrao`, sem lançar. Gravação atômica (`.tmp` e depois `File.Move` com overwrite).
  - `sealed class Inicializacao(IRegistro registro) { bool Ativa(); void Ativar(string caminhoExe); void Desativar(); }`, sobre `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, valor `Vitral`, texto `"<exe>" --inicializacao`.
- Muda: `ServicoRetratos.Salvar` passa a devolver `string` (o nome do arquivo gravado) e ganha `Retrato? Carregar(string nomeArquivo)`.

- [ ] **Passo 1: testes que falham:**
  - `Ler` sem arquivo → `Padrao`;
  - `Gravar` e depois `Ler` devolve o mesmo valor (ida e volta, inclusive com `TesteEmAndamento`);
  - arquivo com `"{lixo"` → `Padrao`;
  - `Ativar(@"C:\x\Vitral.exe")` grava o texto `"C:\x\Vitral.exe" --inicializacao` e `Ativa()` fica verdadeiro;
  - `Desativar()` apaga o valor;
  - `Salvar` devolve um nome que `Carregar` lê de volta, igual ao original.
- [ ] **Passo 2:** `dotnet test` → FAIL (os tipos não existem).
- [ ] **Passo 3:** implementar.
- [ ] **Passo 4:** `dotnet test` → todos PASS.
- [ ] **Passo 5:** commit `feat(core): configuração do app e inicialização com o Windows`.

### Tarefa 2: ServicoVitral (fluxos de alto nível)

**Arquivos:**
- Criar: `src/Vitral.Core/Servicos/ServicoVitral.cs`
- Teste: `tests/Vitral.Core.Tests/ServicoVitralTests.cs`

**Interfaces:**
- Consome: `Aplicador` (com `Aplicar`, `Reverter` e `Restaurar`), `ServicoRetratos`, `RepositorioPerfis`, `ServicoConfig`.
- Produz `sealed class ServicoVitral(Aplicador aplicador, ServicoRetratos retratos, RepositorioPerfis perfis, ServicoConfig config)`, com:
  - `Resultado Aplicar(string perfil)`: aplica e grava `PerfilAtivo`;
  - `Resultado IniciarTeste(string perfil)`: aplica, grava `TesteEmAndamento` com o nome do retrato salvo **antes** de aplicar e **não** muda `PerfilAtivo`;
  - `void ConfirmarTeste()`: `PerfilAtivo = teste.Perfil`, `TesteEmAndamento = null`;
  - `IReadOnlyList<(ValorRegistro, string)> CancelarTeste()`: reverte ao retrato do teste e limpa a marca;
  - `IReadOnlyList<(ValorRegistro, string)> Desfazer()` e `Original()`: usam `Restaurar` da 1a;
  - `EstadoInicial AoIniciar(bool inicializacaoDoWindows)`.
- `sealed record EstadoInicial(bool TesteCancelado, string? Aviso)`. `AoIniciar` faz, em ordem:
  1. Se houver `TesteEmAndamento`, chama `CancelarTeste()` e devolve `TesteCancelado = true` (foco 1).
  2. Senão, se `inicializacaoDoWindows` e houver `PerfilAtivo`, reaplica o perfil. Se o perfil não existir ou for inválido, devolve `Aviso = "O perfil ativo \"X\" não foi encontrado; nada foi aplicado."` sem lançar (foco 3).

- [ ] **Passo 1: testes que falham** (usam `RegistroEmMemoria` e pastas temporárias):
  - `IniciarTeste` e depois `CancelarTeste` deixam o registro como antes e `TesteEmAndamento` nulo;
  - `IniciarTeste` e depois `ConfirmarTeste` → `PerfilAtivo` é o perfil testado;
  - **Foco 1:** `IniciarTeste` com um `ServicoVitral` novo sobre as mesmas pastas (simula o app morto), depois `AoIniciar(false)` → `TesteCancelado` e o registro volta ao de antes;
  - **Foco 3:** `PerfilAtivo = "sumiu"` e `AoIniciar(true)` → `Aviso` contém `"sumiu"`, sem exceção e sem nada gravado no registro;
  - `AoIniciar(true)` com o perfil ativo válido → os valores do perfil aparecem no registro.
- [ ] **Passo 2:** `dotnet test` → FAIL.
- [ ] **Passo 3:** implementar.
- [ ] **Passo 4:** `dotnet test` → PASS.
- [ ] **Passo 5:** commit `feat(core): ServicoVitral com teste seguro e reaplicação no login`.

### Tarefa 3: ViewModels (Vitral.Ui)

**Arquivos:**
- Criar: `src/Vitral.Ui/Vitral.Ui.csproj` (classlib `net10.0-windows`, **sem** `UseWPF`, com o pacote `CommunityToolkit.Mvvm`)
- Criar: `src/Vitral.Ui/IDialogos.cs`, `src/Vitral.Ui/IRelogio.cs`, `src/Vitral.Ui/EditorNativoViewModel.cs`, `src/Vitral.Ui/ContagemViewModel.cs`, `src/Vitral.Ui/PrincipalViewModel.cs`
- Teste: `tests/Vitral.Ui.Tests/` (xUnit): `EditorNativoTests.cs`, `ContagemTests.cs`, `PrincipalTests.cs`

**Interfaces:**
- Consome: `ServicoVitral`, `RepositorioPerfis`, `Pacote`, `AjustesNativosPerfil`.
- Produz:
  - `interface IDialogos { string? EscolherArquivoParaAbrir(string filtro); string? EscolherArquivoParaSalvar(string filtro, string nomeSugerido); bool Confirmar(string pergunta); void Avisar(string mensagem); }`
  - `interface IRelogio { IDisposable ACadaSegundo(Action tique); }`
  - `enum Escolha { NaoMexer, Sim, Nao }`
  - `EditorNativoViewModel`: propriedades observáveis `Escolha Transparencia`, `DestaqueNaBarraEIniciar`, `DestaqueNosTitulos` e `CorAutomatica`; `ModoOpcional ModoSistema` e `ModoApps` (`NaoMexer|Claro|Escuro`); `AlinhamentoOpcional Alinhamento`; `string? CorDestaque` (null = não mexer); `IReadOnlyList<string> CoresSugeridas` (as 18 do esboço da janela: `#0078D4`, `#21375E`, `#0063B1`, `#8764B8`, `#744DA9`, `#E3008C`, `#C30052`, `#E81123`, `#CA5010`, `#FF8C00`, `#FFB900`, `#107C10`, `#00B294`, `#038387`, `#2D7D9A`, `#69797E`, `#567C73`, `#4C4A48`); `void Carregar(AjustesNativosPerfil? a)`; `AjustesNativosPerfil ParaPerfil()`.
  - `ContagemViewModel(int segundos, IRelogio relogio, Action manter, Action voltar)`: `int Restantes`, `RelayCommand Manter` e `RelayCommand Voltar`; ao chegar a 0 chama `voltar` **uma vez só**.
  - `PrincipalViewModel(ServicoVitral, RepositorioPerfis, IDialogos, IRelogio)`: `ObservableCollection<string> Perfis`, `string? Selecionado`, `EditorNativoViewModel Editor`, `string? PerfilAtivo`, `string? Mensagem`, `ContagemViewModel? Contagem`. Comandos: `Novo`, `SalvarPerfil`, `Testar` (cria `Contagem` de 20 s ligada a `ConfirmarTeste` e `CancelarTeste`), `Aplicar`, `Desfazer`, `Original`, `Exportar`, `Importar`.

- [ ] **Passo 1: testes que falham:**
  - **Foco 2:** `Carregar` de um perfil só com `ModoApps = Claro` e depois `ParaPerfil()` devolve `Transparencia == null` e `ModoSistema == null` (não `false` nem `Escuro`);
  - `Escolha.Sim` → `true`, `Escolha.Nao` → `false`;
  - `CorDestaque = "#E81123"` sobrevive à ida e volta; `CorDestaque = "azul"` faz o comando `SalvarPerfil` mostrar aviso, sem salvar;
  - `ContagemViewModel` com relógio falso: 3 tiques de 3 s → `voltar` chamado 1 vez; com `Manter` antes de zerar, `voltar` nunca é chamado e o relógio é liberado;
  - `PrincipalViewModel.Testar` e depois `Contagem.Manter` → `PerfilAtivo` atualizado; `Testar` com contagem esgotada → registro como antes e `Mensagem` com "Voltou ao anterior";
  - **Foco 4:** `Importar` de um pacote com nome que já existe e `IDialogos.Confirmar` devolvendo `false` → perfil local intacto; devolvendo `true` → substituído;
  - `Aplicar` com item bloqueado → `Mensagem` cita o item não aplicado.
- [ ] **Passo 2:** `dotnet test` → FAIL.
- [ ] **Passo 3:** implementar (`ObservableObject`, `[ObservableProperty]` e `[RelayCommand]` do CommunityToolkit).
- [ ] **Passo 4:** `dotnet test` → PASS (as duas suítes).
- [ ] **Passo 5:** commit `feat(ui): view models do editor, contagem e janela principal`.

### Tarefa 4: janela WPF

**Arquivos:**
- Criar: `src/Vitral.App/Vitral.App.csproj` (`WinExe`, `UseWPF`, `AssemblyName=Vitral`, `ApplicationManifest` com dpiAwareness `PerMonitorV2`), `src/Vitral.App/App.xaml(.cs)`, `src/Vitral.App/JanelaPrincipal.xaml(.cs)`, `src/Vitral.App/JanelaContagem.xaml(.cs)`, `src/Vitral.App/DialogosWpf.cs`, `src/Vitral.App/RelogioWpf.cs` (`DispatcherTimer` de 1 s), `src/Vitral.App/Recursos/vitral.ico`

**Interfaces:**
- Consome: `PrincipalViewModel`, `ContagemViewModel`, `IDialogos`, `IRelogio`.
- Layout, conforme o esboço `docs/superpowers/mockups/janela-bandeja.html`:
  - `App.xaml` com `ThemeMode="System"`; janela com Mica (`DWMWA_SYSTEMBACKDROP_TYPE = DWMSBT_MAINWINDOW`).
  - À esquerda: lista de perfis, com o ativo marcado, "+ Novo perfil", Importar/Exportar, "Desfazer última mudança", "Voltar ao Windows original" e, no rodapé, a build do Windows.
  - Abas "Cores e tema" (editor) e "Barra de tarefas", "Iniciar", "Central", "Explorer", "Barra do topo" e "Janelas" (desabilitadas, com o texto "em breve").
  - Rodapé com Descartar · Salvar · **Testar (20 s)** · **Aplicar**.
  - `JanelaContagem`: pequena, sempre no topo, canto inferior direito, com "Manter este visual?", a barra de progresso e "Manter" / "Voltar".
- Na aba do editor, cada opção tem 3 estados: "Não mexer", "Sim" e "Não". A cor tem os 18 quadrados, um seletor livre (`#RRGGBB`) e "Não mexer".

- [ ] **Passo 1:** criar o projeto e as telas ligadas aos ViewModels. `dotnet build -c Release` → `0 Erro(s)`.
- [ ] **Passo 2: verificação manual com o Gustavo** (autorização antes, porque mexe nas cores):
  - abrir o app, criar um perfil com modo dos apps **claro** e destaque **vermelho**, e clicar **Testar**. Esperado: a contagem aparece, as janelas ficam claras e o destaque fica vermelho na tela (anotar se só muda em janelas novas), e em 20 s tudo volta;
  - repetir e clicar **Manter**: o perfil passa a ativo;
  - **Desfazer** volta ao anterior.
- [ ] **Passo 3:** commit `feat(app): janela principal e contagem do testar`.

### Tarefa 5: bandeja, instância única e inicialização

**Arquivos:**
- Criar: `src/Vitral.App/Bandeja.cs`, `src/Vitral.App/InstanciaUnica.cs`
- Modificar: `src/Vitral.App/App.xaml.cs` (argumento `--inicializacao`, `ServicoVitral.AoIniciar`)
- Teste: `tests/Vitral.Ui.Tests/MenuBandejaTests.cs`

**Interfaces:**
- Produz:
  - `Vitral.Ui.MenuBandeja.Montar(IReadOnlyList<string> perfis, string? ativo)` → `IReadOnlyList<ItemMenu>`, com `sealed record ItemMenu(string Texto, string Acao, bool Marcado, bool Separador)`. Ações: `perfil:<nome>`, `desfazer`, `original`, `abrir`, `sair`.
  - `InstanciaUnica`: mutex `Local\Vitral-InstanciaUnica` e um evento nomeado `Local\Vitral-Mostrar`. A segunda instância sinaliza e sai; a primeira traz a janela para a frente.
- Comportamento:
  - Fechar a janela **esconde**; só "Sair" encerra.
  - `--inicializacao` começa só na bandeja.
  - Troca de perfil pela bandeja usa `IniciarTeste` com a contagem de **12 s**.
  - A opção "Iniciar com o Windows" vai na janela, como caixa de seleção ligada a `Inicializacao`.

- [ ] **Passo 1: testes que falham:** `Montar(["A","B"], "B")` → os itens `A` e `B`, com `B` marcado, depois um separador, "Desfazer última mudança", "Voltar ao Windows original", um separador, "Abrir Vitral" e "Sair", nessa ordem.
- [ ] **Passo 2:** `dotnet test` → FAIL.
- [ ] **Passo 3:** implementar `MenuBandeja` (em Vitral.Ui), `Bandeja` (H.NotifyIcon, recriando o menu a cada abertura) e `InstanciaUnica`.
- [ ] **Passo 4:** `dotnet test` → PASS. `dotnet build -c Release` → `0 Erro(s)`.
- [ ] **Passo 5: verificação manual:**
  - abrir o app duas vezes: fica uma janela só (**foco 5**);
  - trocar de perfil pela bandeja: aparece a contagem de 12 s e, sem resposta, volta;
  - marcar "Iniciar com o Windows": o valor `Vitral` aparece em `HKCU\...\Run`;
  - matar o processo durante uma contagem e abrir de novo: volta ao anterior (**foco 1**, no real).
- [ ] **Passo 6:** commit `feat(app): bandeja, instância única e início com o Windows`.

### Tarefa 6: instalador (Velopack) e roteiro do notebook

**Arquivos:**
- Criar: `build.ps1` (raiz), `docs/instalar-no-notebook.md`
- Modificar: `src/Vitral.App/App.xaml.cs` (`VelopackApp.Build().Run()` no início do `Main`)

**Interfaces:**
- `build.ps1 [-Versao 0.1.0]`:
  - roda `dotnet test`;
  - `dotnet publish src/Vitral.App -c Release -r win-x64 --self-contained -o artifacts/publish`;
  - `vpk pack --packId Vitral --packVersion <Versao> --packDir artifacts/publish --mainExe Vitral.exe --outputDir artifacts/release`.
  - Sai com erro se algum passo falhar.

- [ ] **Passo 1:** instalar a ferramenta: `dotnet tool install vpk --tool-path G:\dev\dotnet-tools` (mostrar ao Gustavo o pacote e o tamanho antes); acrescentar `G:\dev\dotnet-tools` ao `tools/env.ps1`.
- [ ] **Passo 2:** escrever o `build.ps1` e rodar. Esperado: `artifacts/release/Vitral-win-Setup.exe` existe e os testes passaram.
- [ ] **Passo 3:** escrever `docs/instalar-no-notebook.md`:
  1. copiar o `Setup.exe` e o arquivo `.vitral` exportado;
  2. rodar o Setup, que instala por usuário, sem admin;
  3. se o SmartScreen avisar, "Mais informações" → "Executar assim mesmo";
  4. Importar → Testar → Aplicar;
  5. como desinstalar.
- [ ] **Passo 4: verificação com o Gustavo** (autorização antes): instalar pelo `Setup.exe` neste PC. O app abre pelo Menu Iniciar e aparece em "Aplicativos instalados". Desinstalar pelo Windows remove o app e mantém `%LOCALAPPDATA%\Vitral` (os perfis dele).
- [ ] **Passo 5:** commit `build: instalador por usuário com Velopack e roteiro do notebook`.
