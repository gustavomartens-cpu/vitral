# Notificações dos apps no notch: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** As notificações dos outros apps descem no notch e ficam num painel do Vitral que desce do sino, com o balão
do Windows quieto.

**Architecture:** Regras puras no Core (`Vitral.Core/Notificacoes`); o aviso no `EstadoNotch` e o painel na Ui; no App,
o pacote esparso dá identidade ao Vitral, o `ServicoNotificacoes` lê pelo `UserNotificationListener` e o
`WindowsQuieto` cala o balão do Windows.

**Tech Stack:** .NET 10 WPF, xUnit, WinRT (`Windows.UI.Notifications.Management`, `Windows.Management.Deployment`).

**Spec:** `docs/superpowers/specs/2026-11-02-notificacoes-design.md` (provas: `docs/superpowers/specs/2026-11-01-prova-central.md`)

## Global Constraints

- Testes: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet test --nologo -v q'`; feche o Vitral antes.
- Português no código e nos textos; commits terminam com `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Pacote esparso: `Name="Vitral"`, `Publisher="CN=Vitral"`, `Application Id="Vitral"`, capacidades `runFullTrust`,
  `unvirtualizedResources`, `userNotificationListener`; `uap10:TrustLevel="mediumIL"`, `RuntimeBehavior="win32App"`,
  `AppListEntry="none"`. Manifesto em `%LOCALAPPDATA%\Vitral\pacote\AppxManifest.xml`.
- Filtro do Claude: `AppId` que começa com `Claude_`.
- Aviso no notch: 6 s (`EstadoNotch.Duracao`); rajada = mais de 3 novas em 2 s → um aviso "{n} notificações novas".
- Textos (exatos): estados do painel
  - `SemIdentidade`: "Ligue o modo de desenvolvedor do Windows (Configurações > Sistema > Para desenvolvedores) para o Vitral mostrar as notificações."
  - `SemPermissao`: "O Windows não deixou o Vitral ler as notificações. Permita em Configurações > Privacidade > Notificações."
  - vazio: "Nenhuma notificação."
  - botões: "Limpar tudo", "Limpar", "Abrir a central do Windows".
- "Há quanto tempo": < 1 min → "agora"; < 60 min → "há {n} min"; < 24 h → "há {n} h"; senão "dd/MM HH:mm".

## Review Focus

1. Vitral fecha por erro com o Windows quieto ligado → o balão do Windows volta (na próxima abertura, no mínimo) (Task 5, `Restaura_o_que_ficou_ligado`).
2. Atualização do Vitral (pasta `current` trocada) → a identidade continua, sem registrar de novo a cada abertura (Task 4, `Nao_registra_de_novo_com_a_mesma_pasta`).
3. Notificação limpa na central do Windows → some do painel e do contador (Task 2, `Diferenca_entre_leituras`).
4. Muitas notificações (100+) ou app sem ícone → painel abre rápido, ícone genérico (Task 2, `Muitas_e_sem_icone`).
5. Permissão negada ou modo de desenvolvedor desligado → o sino volta a abrir a central do Windows e o painel explica (Task 3, `Sem_identidade_o_sino_abre_a_central`).

---

### Task 1: Prova do Windows quieto e do aviso de mudança

**Files:**
- Create: `spike/notificacoes-quieto/` (descartável), resultado em `docs/superpowers/specs/2026-11-01-prova-central.md`
- Modify: `docs/superpowers/specs/2026-11-02-notificacoes-design.md` §3 e §4 (o caminho escolhido)

- [ ] **Step 1:** Com o pacote `VitralProva` registrado de novo (`spike/identidade`), conferir se `UserNotificationListener.NotificationChanged` dispara num exe desktop com identidade, mandando uma notificação de teste (o próprio exe gera uma com `ToastNotificationManager`). Registrar sim/não.
- [ ] **Step 2:** Testar os caminhos do Windows quieto, na ordem da spec §4, com uma notificação de teste a cada um: (1) desligar o banner de um app pela configuração de notificações do Windows (achar a chave/valor em `HKCU\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\<AUMID>` comparando antes/depois de desligar "Mostrar banners" pela tela de Configurações), conferindo que a notificação ainda chega ao `GetNotificationsAsync`; (2) o "não perturbe"; (3) não testar se (1) ou (2) bastar. Registrar o caminho escolhido e como desfazer.
- [ ] **Step 3:** Remover o pacote de teste; registrar os resultados na prova e na spec; commit `spike: Windows quieto e aviso de mudança das notificações`.
- [ ] **Parada:** nenhum caminho funcionou → perguntar ao Gustavo antes da Task 5.

### Task 2: Regras das notificações (Core)

**Files:**
- Create: `src/Vitral.Core/Notificacoes/NotificacaoApp.cs`, `src/Vitral.Core/Notificacoes/RegrasNotificacoes.cs`
- Test: `tests/Vitral.Core.Tests/NotificacoesTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record NotificacaoApp(uint Id, string AppId, string NomeApp, string? Titulo, string? Texto, DateTimeOffset Chegou)`
  - `public sealed record GrupoNotificacoes(string AppId, string NomeApp, IReadOnlyList<NotificacaoApp> Itens)`
  - `public enum EstadoNotificacoes { Ligado, SemIdentidade, SemPermissao }`
  - `static class RegrasNotificacoes`: `IReadOnlyList<NotificacaoApp> Filtrar(IEnumerable<NotificacaoApp>)` (tira o Claude), `IReadOnlyList<GrupoNotificacoes> Agrupar(IEnumerable<NotificacaoApp>)` (grupo com a mais recente primeiro; itens mais recentes primeiro), `(IReadOnlyList<NotificacaoApp> Novas, IReadOnlyList<uint> Sumidas) Diferenca(IReadOnlyCollection<NotificacaoApp> antes, IReadOnlyCollection<NotificacaoApp> agora)`, `string HaQuanto(DateTimeOffset chegou, DateTimeOffset agora)`, `string? Texto(EstadoNotificacoes)` (nulo para `Ligado`).
- [ ] **Step 1: Testes** `Filtra_o_app_do_Claude`, `Agrupa_por_app_com_o_mais_recente_em_cima`, `Diferenca_entre_leituras` (nova entra, limpa no Windows sai), `Ha_quanto_tempo` (Theory: 30 s → "agora", 5 min → "há 5 min", 3 h → "há 3 h", 2 dias → "dd/MM HH:mm"), `Textos_dos_estados`, `Muitas_e_sem_icone` (150 itens de 20 apps agrupam em < 50 ms; nome vazio vira o AppId).
- [ ] **Step 2:** Ver falhar. **Step 3:** Implementar. **Step 4:** Suíte verde. **Step 5:** Commit `feat: regras das notificações`.

### Task 3: Aviso no notch e painel (Ui)

**Files:**
- Create: `src/Vitral.Ui/Barra/EstadoNotch.Notificacoes.cs`, `src/Vitral.Ui/Barra/PainelNotificacoesViewModel.cs`
- Modify: `src/Vitral.Ui/Barra/CoordenadorPaineis.cs` (`Painel.Notificacoes`), `src/Vitral.Ui/Barra/EstadoNotch.cs` (`AcaoNotch.AbrirPainelNotificacoes`; `Tique` encerra o aviso de notificação em 6 s; `Clicou` no aviso de notificação devolve `AbrirApp`)
- Test: `tests/Vitral.Ui.Tests/NotchNotificacoesTests.cs`, `tests/Vitral.Ui.Tests/PainelNotificacoesTests.cs`

**Interfaces:**
- Consumes: Task 2.
- Produces:
  - `public sealed record AvisoNotificacao(string AppId, string NomeApp, string? Titulo, string? Texto, int Quantas = 1)`
  - `EstadoNotch`: `void Notificou(AvisoNotificacao)`, `AvisoNotificacao? AvisoNotificacaoAtual`, `bool NotificacoesLigadas { get; set; }`, `int Contador { get; set; }`; `AcaoNotch.AbrirApp` (clique no aviso) e `AcaoNotch.AbrirPainelNotificacoes` (sino, com `NotificacoesLigadas`; senão continua `AbrirNotificacoes`).
  - `PainelNotificacoesViewModel(Func<DateTimeOffset> agora)`: `void Atualizar(EstadoNotificacoes, IReadOnlyList<NotificacaoApp>)`, `IReadOnlyList<GrupoNotificacoes> Grupos`, `string? TextoEstado`, `bool Vazio`, eventos `PediuLimpar(uint)`, `PediuLimparApp(string)`, `PediuLimparTudo`, `PediuAbrirApp(string)`, `PediuCentral`; comandos `Limpar`, `LimparApp`, `LimparTudo`, `AbrirApp`, `AbrirCentral`.
- [ ] **Step 1: Testes** (notch) `Notificacao_desce_e_sai_em_6s`, `Espera_a_ficha_de_permissao`, `Fila_com_aviso_do_Claude_por_chegada`, `Clique_no_aviso_abre_o_app`, `Sino_abre_o_painel_quando_ligado`, `Sem_identidade_o_sino_abre_a_central`; (painel) `Agrupa_e_mostra_o_estado`, `Limpar_um_grupo_e_tudo_pedem_ao_servico`, `Vazio_mostra_nenhuma`.
- [ ] **Step 2:** Ver falhar. **Step 3:** Implementar. **Step 4:** Suíte verde. **Step 5:** Commit `feat: aviso de notificação no notch e painel`.

### Task 4: Identidade de pacote do Vitral

**Files:**
- Create: `src/Vitral.App/Notificacoes/PacoteVitral.cs`, `src/Vitral.App/Recursos/PacoteLogo.png` (o ícone do Vitral 150×150)
- Modify: `src/Vitral.App/app.manifest` (elemento `<msix>`), `src/Vitral.App/Vitral.App.csproj` (logo copiado para a saída), `src/Vitral.Core/Notificacoes/RegrasNotificacoes.cs` (`static bool PrecisaRegistrar(string? pastaRegistrada, string pastaAtual)`)
- Test: `tests/Vitral.Core.Tests/NotificacoesTests.cs`

**Interfaces:**
- Produces: `internal static class PacoteVitral { static Task<EstadoNotificacoes> Garantir(); static bool TemIdentidade { get; } }` (`Package.Current` sem exceção).
- [ ] **Step 1: Teste** `Nao_registra_de_novo_com_a_mesma_pasta`: `PrecisaRegistrar("C:\\a\\current", "C:\\a\\current")` false; pasta diferente ou nula → true; comparação sem diferenciar maiúsculas.
- [ ] **Step 2:** Ver falhar; implementar `PrecisaRegistrar`; verde.
- [ ] **Step 3:** `PacoteVitral.Garantir`: sem modo de desenvolvedor (`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock\AllowDevelopmentWithoutDevLicense` ≠ 1) → `SemIdentidade`; grava o manifesto (Global Constraints) e o logo em `%LOCALAPPDATA%\Vitral\pacote\`; lê o pacote `Vitral` registrado (`PackageManager.FindPackagesForUser("", "Vitral_<publisherId>")`, pasta externa pela `EffectiveExternalLocation`); se `PrecisaRegistrar`, `RegisterPackageByUriAsync(manifesto, new RegisterPackageOptions { ExternalLocationUri = pasta do exe, DeveloperMode = true })`. O pacote passa a valer na próxima abertura do Vitral: com identidade agora → `Ligado`; registrado agora sem identidade → reabrir sozinho uma vez (`Process.Start` do próprio exe com `--reaberto`, e o atual sai pelo caminho do Sair).
- [ ] **Step 4: Roteiro na máquina** com o Vitral de desenvolvimento: primeira abertura registra e reabre; a segunda tem identidade (log); `Get-AppxPackage Vitral` mostra a pasta do exe. Build sem avisos.
- [ ] **Step 5:** Commit `feat: identidade de pacote do Vitral`.

### Task 5: Serviço das notificações e Windows quieto

**Files:**
- Create: `src/Vitral.App/Notificacoes/ServicoNotificacoes.cs`, `src/Vitral.App/Notificacoes/WindowsQuieto.cs`
- Modify: `src/Vitral.Core/Configuracao/ConfigApp.cs` (`IReadOnlyList<string>? BannersDesligados = null`: o que o Vitral calou, para devolver), `src/Vitral.App/App.xaml.cs` (criar depois do `PacoteVitral.Garantir`; devolver no Sair, no `SessionEnding` e no erro fatal; restaurar na abertura o que ficou calado)
- Test: `tests/Vitral.Core.Tests/NotificacoesTests.cs`

**Interfaces:**
- Consumes: Tasks 2 e 4; o caminho escolhido na Task 1.
- Produces: `ServicoNotificacoes`: `event Action<EstadoNotificacoes, IReadOnlyList<NotificacaoApp>>? Mudou`, `event Action<IReadOnlyList<NotificacaoApp>>? Novas`, `Task Limpar(uint)`, `Task LimparApp(string)`, `Task LimparTudo()`, `void AbrirApp(string appId)`, `Task<ImageSource?> Icone(string appId)` (cache por app). `WindowsQuieto`: `void Calar(IEnumerable<string> appIds)`, `void Devolver()`, com o que calou gravado em `ConfigApp.BannersDesligados`. Regra pura no Core: `static IReadOnlyList<string> RegrasNotificacoes.ParaDevolver(IReadOnlyList<string>? gravados)`.
- [ ] **Step 1: Teste** `Restaura_o_que_ficou_ligado`: com `BannersDesligados = ["A","B"]` no config, `ParaDevolver` devolve os dois; com nulo, vazio.
- [ ] **Step 2:** Ver falhar; implementar; verde.
- [ ] **Step 3:** `ServicoNotificacoes` conforme a spec §3 e o resultado da Task 1 (evento ou vigia do `wpndatabase.db-wal` com intervalo mínimo de 1 s), filtro e diferença pela Task 2; leitura fora da thread da interface; `Mudou`/`Novas` publicados na thread da interface. `AbrirApp`: `explorer.exe shell:AppsFolder\<AUMID>`. `WindowsQuieto` pelo caminho da Task 1, calando cada app que aparece e devolvendo na saída.
- [ ] **Step 4: Roteiro na máquina:** notificação de teste (um `ToastNotificationManager` do próprio Vitral de desenvolvimento, só com `--teste-notificacao`) chega ao serviço; balão do Windows não aparece; `Stop-Process -Force` no Vitral e abrir de novo → o balão volta. Build sem avisos.
- [ ] **Step 5:** Commit `feat: serviço das notificações e Windows quieto`.

### Task 6: Painel e sino na barra do topo

**Files:**
- Create: `src/Vitral.App/Barra/PainelNotificacoes.cs` (no molde de `PainelPendencias.cs`)
- Modify: `src/Vitral.App/Barra/ControleBarra.cs` (`Painel.Notificacoes` no `switch` de criação, alinhado ao notch; avisos novos → `EstadoNotch.Notificou`, com a rajada da Task 2), `src/Vitral.App/Barra/JanelaNotch.cs` (o sino da lateral direita com o contador; `AbrirPainelNotificacoes`; `AbrirApp` no clique do aviso; desenho do aviso de notificação com ícone, app, título e texto), `src/Vitral.App/App.xaml.cs` (ligar serviço ↔ notch ↔ painel)

- [ ] **Step 1:** Painel e sino conforme a spec §5 e §6, com os textos das Global Constraints.
- [ ] **Step 2:** Build sem avisos; suíte verde.
- [ ] **Step 3:** Commit `feat: painel de notificações no notch`.

### Task 7: Roteiro final e documentos

- [ ] **Step 1:** Com o Gustavo: notificação nova desce no notch sem o balão do Windows; o sino conta e abre o painel; limpar um, um app e tudo (some também da central do Windows); abrir o app; notificação do app do Claude não aparece; fechar o Vitral pela bandeja devolve o balão.
- [ ] **Step 2:** Notas da versão, `docs/ideias-pendentes.md` (tira "Notificações de outros apps no notch"; "Já feito"); commit `docs: notificações no notch`.
