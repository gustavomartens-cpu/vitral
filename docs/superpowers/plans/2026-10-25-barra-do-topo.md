# Barra do topo — Plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Três ilhas de vidro (mídia, relógio, status) no topo do monitor principal, com espaço reservado (AppBar de 46 px), ligadas e desligadas pelo perfil (esquema 2) e revertidas junto com o registro.

**Architecture:** O Core ganha o modelo `BarraTopoPerfil`, o esquema 2 do perfil e o estado da barra em `config.json` e nos retratos, aplicado por uma interface `IControleBarra`. O Ui ganha as fontes de dados como interfaces e os ViewModels das ilhas, testados com fontes falsas. O App implementa as fontes com APIs do Windows, a reserva (`SHAppBarMessage`) e as janelas das ilhas, com a técnica de vidro escolhida no teste da Task 1.

**Tech Stack:** .NET 10, WPF, CommunityToolkit.Mvvm 8.4.2, xUnit; WinRT por TFM `net10.0-windows10.0.19041.0`; Win32/COM por `LibraryImport`/`ComImport`. Nenhum pacote NuGet novo.

**Spec:** `docs/superpowers/specs/2026-10-24-identidade-e-barra-do-topo-design.md` (§3 a §8; §2 para tokens visuais).

## Global Constraints

- Ambiente: `. tools/env.ps1` antes de `dotnet`; caches e ferramentas só em `G:\dev`. Nada de instalar Node ou pacotes fora disso.
- Ilhas: 30 px de altura, 8 px do topo; mídia e status a 12 px das bordas; relógio centralizado. Reserva `ABE_TOP` de **46 px**, só no monitor principal.
- Vidro das ilhas: `Noite` ~72% (`NoiteVidro` #B80A0F20), `Borda`, `Especular`, raio pílula; fallback sem desfoque `Noite` a 85% (#D90A0F20) só com aprovação do Gustavo.
- Esquema do perfil passa a 2: `"barraTopo": { "visivel": true, "ilhas": { "midia": true, "relogio": true, "status": true, "desempenho": true } }`. `barraTopo` ausente/null = não mexer; `ilhas` ausente (ou campo ausente) = ligado.
- Fonte indisponível mostra "—" (travessão, U+2014). Fontes nunca lançam para fora; erros vão para `%LOCALAPPDATA%\Vitral\logs\barra.log`.
- Volume: ±2 por passo da roda, limitado a 0..100.
- Relógio `HH:mm`; data curta pt-BR sem pontos e minúscula: `sex, 23 out`.
- Texto em português do Brasil em toda a interface.
- Toda mudança real no Windows (aplicar perfil, testar) e toda instalação só com autorização do Gustavo na hora.

## Review Focus

1. **Explorer reinicia com a barra ligada** → reserva e ilhas voltam sozinhas (`TaskbarCreated`). Verificação manual na Task 8.
2. **Retratos antigos**: retrato sem `barraTopo` restaura como "não mexer"; o `original.json` antigo (anterior à barra) restaura como **desligada**, porque o Windows original nunca teve barra. Testes na Task 3.
3. **Primeira leitura de CPU** (precisa de duas amostras) e fontes que devolvem null → "—", nunca 0% falso nem exceção. Testes na Task 4.
4. **Sessão de mídia sem título ou que termina no meio** → a ilha mostra o nome do app ou some, sem exceção. Testes na Task 4.
5. **Janela maximizada e mudança de escala/monitor** → começa abaixo das ilhas e as ilhas se reposicionam. Teste da posição na Task 6, verificação manual na Task 8.

---

### Task 1: Teste de vidro (portão)

Descartável: o código fica no scratchpad, não no repositório. Só a decisão entra.

**Files:**
- Create (scratchpad): `<scratchpad>\spike-vidro\` (projeto WPF com três janelas-ilha de 300×30, uma por técnica)
- Modify: este plano, seção "Decisão do vidro" ao fim da Task 1; ledger `.superpowers/sdd/2026-10-25-barra-do-topo/progress.md`

- [ ] **Step 1: Montar as três janelas**, cada uma `WindowStyle=None`, `AllowsTransparency=False`, `Topmost`, `ShowActivated=False`, `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, cantos arredondados, um texto "Técnica N" em `Texto`:
  1. `SetWindowCompositionAttribute(WCA_ACCENT_POLICY=19)` com `ACCENT_ENABLE_BLURBEHIND=3`, `GradientColor` 0xB8200F0A (ABGR de `NoiteVidro`);
  2. o mesmo com `ACCENT_ENABLE_ACRYLICBLURBEHIND=4`;
  3. `DWMWA_USE_HOSTBACKDROPBRUSH=17` = 1 + `DWMWA_SYSTEMBACKDROP_TYPE=38` = 3, `CompositionTarget.BackgroundColor` transparente, fundo `NoiteVidro`.
- [ ] **Step 2: Rodar e verificar com o Gustavo na tela** (abrir pelo `explorer.exe` para não herdar janela minimizada): com outra janela ativa por cima do papel de parede claro e do escuro; com a economia de bateria/energia ligada e desligada (pedir que ele mude; não mexer sozinho). Critério: desfoque visível com a janela **inativa**, sem tremer ao arrastar outra janela por baixo.
- [ ] **Step 3: Registrar a decisão** aqui e no ledger: técnica escolhida e parâmetros exatos. Se nenhuma mantiver o desfoque, propor o fallback (Noite 85% sem desfoque) e **esperar a aprovação do Gustavo** antes da Task 6.
- [ ] **Step 4: Commit** só do plano e do ledger: `docs: decisão do vidro das ilhas`.

**Decisão do vidro:** _(preencher no Step 3)_

---

### Task 2: Modelo da barra e esquema 2 do perfil

**Files:**
- Create: `src/Vitral.Core/Perfis/BarraTopoPerfil.cs`
- Modify: `src/Vitral.Core/Perfis/Perfil.cs`, `src/Vitral.Core/Perfis/PerfilJson.cs`
- Test: `tests/Vitral.Core.Tests/PerfilJsonTests.cs`, `tests/Vitral.Core.Tests/PacoteTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record IlhasBarra(bool Midia = true, bool Relogio = true, bool Status = true, bool Desempenho = true) { public static IlhasBarra Todas { get; } }`
  - `public sealed record BarraTopoPerfil(bool Visivel, IlhasBarra? Ilhas = null) { public IlhasBarra IlhasEfetivas => Ilhas ?? IlhasBarra.Todas; public static BarraTopoPerfil Desligada { get; } = new(false); }`
  - `Perfil(int Esquema, string Nome, string? BuildTestada, AjustesNativosPerfil? Nativo, BarraTopoPerfil? BarraTopo = null)` (parâmetro opcional: os construtores existentes continuam valendo)
  - `PerfilJson.EsquemaSuportado = 2`; `PerfilJson.Escrever` grava sempre `esquema: 2`.

- [ ] **Step 1: Testes que falham** em `PerfilJsonTests`:
  - `Esquema2_ida_e_volta`: perfil com `BarraTopo = new(true, new IlhasBarra(Desempenho: false))` → `Escrever` → `Ler` devolve igual; o JSON contém `"esquema": 2` e `"desempenho": false`.
  - `Esquema1_le_com_barra_nula`: JSON `{"esquema":1,"nome":"A","nativo":{}}` → `BarraTopo` null; `Escrever` desse perfil contém `"esquema": 2`.
  - `Ilhas_ausente_e_campo_ausente_valem_ligado`: `"barraTopo":{"visivel":true}` → `IlhasEfetivas == IlhasBarra.Todas`; `"barraTopo":{"visivel":true,"ilhas":{"midia":false}}` → `Midia` false e as outras true.
  - `Esquema3_recusado`: mensagem cita "esquema 3" e "até o esquema 2".
  - Em `PacoteTests`: `Importa_pacote_com_perfil_esquema1` (zip montado à mão com `perfil.json` de esquema 1) → importado com `BarraTopo` null.
- [ ] **Step 2: Rodar** `dotnet test tests/Vitral.Core.Tests --filter "PerfilJsonTests|PacoteTests"` → FAIL (`BarraTopoPerfil` não existe).
- [ ] **Step 3: Implementar** os records e o esquema 2. `Escrever(Perfil p)` serializa `p with { Esquema = EsquemaSuportado }`. Atualizar o comentário do topo de `PerfilJson` (a seção "barra" de fases futuras deixa de ser ignorada; agora é `barraTopo`).
- [ ] **Step 4: Rodar** a suíte Core inteira → PASS (os testes antigos que conferem `esquema: 1` na escrita, se houver, passam a esperar 2).
- [ ] **Step 5: Commit** `feat(core): perfil esquema 2 com barraTopo`.

---

### Task 3: Estado da barra no config, nos retratos e nos fluxos

**Files:**
- Create: `src/Vitral.Core/Barra/IControleBarra.cs`, `src/Vitral.Core/Barra/EstadoBarra.cs`
- Modify: `src/Vitral.Core/Configuracao/ConfigApp.cs`, `src/Vitral.Core/Retratos/Retrato.cs`, `src/Vitral.Core/Retratos/ServicoRetratos.cs`, `src/Vitral.Core/Aplicacao/Aplicador.cs`, `src/Vitral.Core/Servicos/ServicoVitral.cs`
- Test: `tests/Vitral.Core.Tests/BarraTopoTests.cs` (com `ControleBarraFalso` que guarda as chamadas)

**Interfaces:**
- Consumes: `BarraTopoPerfil`, `IlhasBarra` (Task 2).
- Produces:
  - `public interface IControleBarra { void Aplicar(BarraTopoPerfil estado); }` — contrato: nunca lança (a implementação real registra e avisa).
  - `public sealed class EstadoBarra(IControleBarra controle, ServicoConfig config)` com `BarraTopoPerfil Atual()` (`config.BarraTopo ?? Desligada`) e `void Aplicar(BarraTopoPerfil? estado)` (null = nada; senão `controle.Aplicar` e grava em `config.json`).
  - `ConfigApp(..., TesteEmAndamento? TesteEmAndamento, BarraTopoPerfil? BarraTopo = null)`.
  - `Retrato(DateTimeOffset Quando, string? PerfilAplicado, IReadOnlyList<ValorRegistro> Valores, BarraTopoPerfil? BarraTopo = null)`; null = "não mexer" ao reverter.
  - `ServicoRetratos(IRegistro registro, string pasta, Func<BarraTopoPerfil>? barraAtual = null)`: `Tirar` preenche `BarraTopo = barraAtual?.Invoke()`. Serializa `barraTopo` no JSON do retrato. **`GarantirOriginal` lendo um `original.json` sem `barraTopo` devolve `BarraTopo = Desligada`.**
  - `Aplicador(IRegistro, ServicoRetratos, INotificador, EstadoBarra? barra = null)`: `Aplicar(p)` chama `barra?.Aplicar(p.BarraTopo)` depois do registro; `Reverter(r)` chama `barra?.Aplicar(r.BarraTopo)`. Se a gravação do registro falhar e houver reversão, a barra não é tocada.
  - `ServicoVitral.BarraAtual()` → `BarraTopoPerfil` (lida do config), usada pelo App ao abrir.

- [ ] **Step 1: Testes que falham** em `BarraTopoTests` (registro em memória, pastas temporárias, `EstadoBarra` com `ControleBarraFalso` e o mesmo `ServicoConfig`):
  - `Aplicar_perfil_com_barra_liga_e_grava_no_config`: chamadas == `[new(true)]`; `config.Ler().BarraTopo == new(true)`.
  - `Perfil_sem_barra_nao_mexe`: nenhuma chamada; config continua null.
  - `Cancelar_teste_volta_ao_estado_anterior`: config começa com barra desligada; `IniciarTeste` de perfil com barra ligada; `CancelarTeste` → última chamada `Desligada` e config `Desligada`.
  - `Desfazer_e_original_levam_a_barra`: aplicar ligada → `Desfazer` → última chamada `Desligada`; aplicar ligada de novo → `Original` → `Desligada`.
  - `Retrato_antigo_sem_barra_nao_mexe`: arquivo de retrato JSON escrito à mão sem `barraTopo` → `Reverter` não chama o controle.
  - `Original_antigo_sem_barra_vale_desligada`: `original.json` escrito à mão sem `barraTopo` → `GarantirOriginal().BarraTopo == Desligada`.
  - `Retrato_grava_e_le_barra`: `Salvar` + `Carregar` preserva `BarraTopo` com ilhas.
  - `Config_antigo_sem_barra_le_null`: `config.json` sem o campo → `BarraTopo` null.
- [ ] **Step 2: Rodar** `dotnet test tests/Vitral.Core.Tests --filter BarraTopoTests` → FAIL (tipos inexistentes).
- [ ] **Step 3: Implementar** conforme Interfaces. `ServicoVitral` não muda de construtor; `BarraAtual()` lê `config.Ler().BarraTopo ?? BarraTopoPerfil.Desligada`. O CLI continua sem barra (`Aplicador` sem `EstadoBarra`): perfis aplicados pelo CLI não mexem na barra.
- [ ] **Step 4: Rodar** suítes Core e Ui inteiras → PASS.
- [ ] **Step 5: Commit** `feat(core): estado da barra do topo no config e nos retratos`.

---

### Task 4: Fontes (interfaces) e ViewModels das ilhas

**Files:**
- Create: `src/Vitral.Ui/Barra/Fontes.cs`, `src/Vitral.Ui/Barra/IlhaMidiaViewModel.cs`, `src/Vitral.Ui/Barra/IlhaRelogioViewModel.cs`, `src/Vitral.Ui/Barra/IlhaStatusViewModel.cs`
- Test: `tests/Vitral.Ui.Tests/IlhasTests.cs`, fontes falsas em `tests/Vitral.Ui.Tests/FontesFalsas.cs`

**Interfaces:**
- Consumes: `IRelogio` (existente), `IlhasBarra` (Task 2).
- Produces (em `Fontes.cs`; eventos sempre disparados na thread da interface — responsabilidade da implementação real):
  - `public sealed record EstadoMidia(string? Titulo, string? Artista, string NomeApp, bool Tocando, byte[]? Capa);`
  - `public interface IFonteMidia { EstadoMidia? Atual { get; } event Action? Mudou; void Anterior(); void TocarPausar(); void Proxima(); void TrazerApp(); }`
  - `public interface IFonteVolume { int? Volume { get; } bool? Mudo { get; } event Action? Mudou; void Definir(int volume); void AlternarMudo(); }`
  - `public enum TipoRede { Desconectada, Wifi, Cabo }` e `public interface IFonteRede { TipoRede? Atual { get; } event Action? Mudou; }`
  - `public sealed record EstadoBateria(int Porcentagem, bool Carregando);` e `public interface IFonteBateria { EstadoBateria? Atual { get; } event Action? Mudou; }` (null = sem bateria ou indisponível)
  - `public sealed record Desempenho(double Cpu, double Memoria);` (0..1) e `public interface IFonteDesempenho { Desempenho? Ler(); }` (null na primeira leitura de CPU e em falha)
  - `public interface IRelogioDoSistema { DateTime Agora { get; } }`
  - `IlhaMidiaViewModel(IFonteMidia fonte)`: `bool Visivel`, `string Titulo`, `bool Tocando`, comandos `Anterior`, `TocarPausar`, `Proxima`, `TrazerApp`. `const int LimiteTitulo = 32`.
  - `IlhaRelogioViewModel(IRelogioDoSistema agora, IRelogio relogio)`: `string Hora`, `string Data`; `IDisposable`.
  - `IlhaStatusViewModel(IFonteDesempenho, IFonteVolume, IFonteRede, IFonteBateria, IRelogio, IlhasBarra ilhas)`: `string Cpu`, `double CpuFracao`, `string Memoria`, `double MemoriaFracao`, `bool MostrarDesempenho`, `string Volume`, `bool Mudo`, `TipoRede? Rede`, `string Bateria`, `bool BateriaVisivel`, `void Rolar(int passos)`, comando `AlternarMudo`; `IDisposable`; `const string SemDado = "—"`.

- [ ] **Step 1: Testes que falham** em `IlhasTests`:
  - `Midia_some_sem_sessao` / `Midia_aparece_e_some_com_o_evento`: `Atual = null` → `Visivel` false; muda para estado e dispara `Mudou` → true; volta a null → false.
  - `Titulo_longo_truncado`: título de 40 caracteres → 32 caracteres terminando em "…".
  - `Titulo_vazio_usa_nome_do_app`: `Titulo = null` ou `""` e `NomeApp = "Spotify"` → `"Spotify"`; com artista → `"Título — Artista"`.
  - `Comandos_de_midia_chegam_na_fonte`.
  - `Relogio_em_ptBR`: `Agora = 2026-10-23 09:05` → `Hora == "09:05"`, `Data == "sex, 23 out"`; `2026-05-04` → `"seg, 4 mai"`; tique do relógio atualiza.
  - `Cpu_sem_leitura_mostra_traco`: `Ler()` devolve null → `Cpu == "—"`, `CpuFracao == 0`; depois `new(0.37, 0.62)` e um tique → `"37%"` e `"62%"`.
  - `Desempenho_desligado_esconde`: `ilhas with { Desempenho = false }` → `MostrarDesempenho` false.
  - `Volume_null_mostra_traco_e_nao_quebra`: `Volume = null` → `"—"`; `Rolar(1)` e `AlternarMudo` não lançam e não chamam `Definir`.
  - `Roda_do_volume_soma_2_e_limita`: 50 + `Rolar(1)` → `Definir(52)`; 99 + `Rolar(1)` → `Definir(100)`; 1 + `Rolar(-1)` → `Definir(0)`; `Rolar(-3)` em 50 → `Definir(44)`.
  - `Bateria_so_com_bateria`: null → `BateriaVisivel` false; `new(81, false)` → `"81%"`, true.
  - `Dispose_para_o_relogio`: `RelogioFalso.Liberado` true.
- [ ] **Step 2: Rodar** `dotnet test tests/Vitral.Ui.Tests --filter IlhasTests` → FAIL.
- [ ] **Step 3: Implementar.** Data: `CultureInfo("pt-BR")`, formato `"ddd, d MMM"`, depois `.Replace(".", "").ToLowerInvariant()`. Porcentagens com `Math.Round(x * 100)`. VMs são `ObservableObject` e se inscrevem nos eventos das fontes (`Dispose` desinscreve).
- [ ] **Step 4: Rodar** suíte Ui inteira → PASS.
- [ ] **Step 5: Commit** `feat(ui): fontes e viewmodels das ilhas da barra do topo`.

---

### Task 5: Aba "Barra do topo" no editor de perfil

**Files:**
- Create: `src/Vitral.Ui/EditorBarraViewModel.cs`
- Modify: `src/Vitral.Ui/PrincipalViewModel.cs` (carregar, salvar, descartar incluem a barra), `src/Vitral.App/JanelaPrincipal.xaml` (habilitar a aba "Barra do topo")
- Test: `tests/Vitral.Ui.Tests/EditorBarraTests.cs`

**Interfaces:**
- Consumes: `BarraTopoPerfil`, `IlhasBarra`, `Escolha`, `Opcoes.Ligada` (existentes).
- Produces: `EditorBarraViewModel` com `Escolha Barra` (NaoMexer/Sim/Nao), `bool Midia`, `bool Relogio`, `bool Status`, `bool Desempenho`, `bool IlhasHabilitadas` (só com `Barra == Sim`), `void Carregar(BarraTopoPerfil? b)`, `BarraTopoPerfil? ParaPerfil()` (NaoMexer → null; Nao → `Desligada`; Sim → `new(true, new IlhasBarra(...))`). `PrincipalViewModel.EditorBarra { get; }`.

- [ ] **Step 1: Testes que falham**:
  - `Carregar_e_devolver_ida_e_volta` para null, `Desligada` e ligada com `Desempenho: false`.
  - `Salvar_grava_barra_no_perfil`: selecionar perfil, `EditorBarra.Barra = Sim`, `Desempenho = false`, Salvar → `Perfis.Carregar` tem `BarraTopo == new(true, new IlhasBarra(Desempenho: false))` e o arquivo tem `esquema: 2`.
  - `Salvar_preserva_nativo`: os ajustes nativos editados continuam iguais.
  - `Descartar_volta_a_barra_salva`.
- [ ] **Step 2: Rodar** `--filter EditorBarraTests` → FAIL.
- [ ] **Step 3: Implementar.** `SalvarEditor` monta `new Perfil(PerfilJson.EsquemaSuportado, nome, build, ajustes, EditorBarra.ParaPerfil())`; `OnSelecionadoChanged` e `Descartar` carregam os dois editores. Na XAML, a aba "Barra do topo" deixa de ser `IsEnabled="False"` e mostra um `CartaoVidro` com `SeletorPilula` (`Opcoes.Ligada`, rótulo "Barra do topo") e quatro `CheckBox` ("Mídia tocando", "Relógio", "Status", "CPU e memória") com `IsEnabled` em `IlhasHabilitadas`.
- [ ] **Step 4: Rodar** suítes inteiras → PASS; abrir o app (pelo `explorer.exe`) e conferir a aba com screenshot.
- [ ] **Step 5: Commit** `feat(ui): aba Barra do topo no editor de perfil`.

---

### Task 6: Reserva, janelas das ilhas e controle real

**Files:**
- Modify: `src/Vitral.App/Vitral.App.csproj` (TFM `net10.0-windows10.0.19041.0`; `SupportedOSPlatformVersion` 10.0.19041.0), `src/Vitral.App/App.xaml.cs`
- Create: `src/Vitral.App/Barra/ReservaTopo.cs`, `src/Vitral.App/Barra/JanelaIlha.xaml(.cs)`, `src/Vitral.App/Barra/VidroIlha.cs`, `src/Vitral.App/Barra/ControleBarra.cs`, `src/Vitral.App/Barra/LogBarra.cs`, `src/Vitral.Ui/Barra/PosicaoIlhas.cs`
- Test: `tests/Vitral.Ui.Tests/PosicaoIlhasTests.cs`

**Interfaces:**
- Consumes: `IControleBarra`, `EstadoBarra`, `ServicoVitral.BarraAtual()` (Task 3); decisão do vidro (Task 1).
- Produces:
  - `public readonly record struct Retangulo(double X, double Y, double Largura, double Altura);`
  - `public static class PosicaoIlhas { public const double Topo = 8, Borda = 12, Altura = 30, Reserva = 46; public static (Retangulo Midia, Retangulo Relogio, Retangulo Status) Calcular(Retangulo monitor, double larguraMidia, double larguraRelogio, double larguraStatus); }` — `monitor` são os limites inteiros do monitor principal em DIPs (não a área de trabalho, que já começa abaixo da reserva).
  - `ReservaTopo : IDisposable` — `bool Registrar()` (ABM_NEW + QUERYPOS/SETPOS com 46 px em DIP × escala), `void Reposicionar()`, `void Liberar()` (ABM_REMOVE), `event Action<bool>? TelaCheia`, `event Action? Recriar` (callback da AppBar em janela oculta `HwndSource`).
  - `ControleBarra(LogBarra log, Action<string> avisar) : IControleBarra` — abre/fecha/ajusta as 3 `JanelaIlha`; `Visivel=false` fecha tudo e libera a reserva; falha de `Registrar` mostra as ilhas mesmo assim e chama `avisar("Não foi possível reservar o espaço da barra do topo.")`. Nunca lança.
  - `LogBarra` — `void Erro(string origem, Exception e)` em `%LOCALAPPDATA%\Vitral\logs\barra.log` (`Pastas.Logs`), sem lançar.

- [ ] **Step 1: Testes que falham** em `PosicaoIlhasTests`: monitor `(0,0,1920,1032)`, larguras 300/140/360 → mídia `(12,8,300,30)`, relógio `(890,8,140,30)`, status `(1548,8,360,30)`; monitor `(-1080,-477,1080,1920)` com larguras 300/140/360 → mídia `(-1068,-469,300,30)`, relógio `(-610,-469,140,30)`, status `(-372,-469,360,30)`.
- [ ] **Step 2: Rodar** `--filter PosicaoIlhasTests` → FAIL.
- [ ] **Step 3: Implementar `PosicaoIlhas`** e rodar → PASS.
- [ ] **Step 4: Implementar App:** trocar o TFM e rodar build + suítes (0 avisos); `JanelaIlha` (`WindowStyle=None`, `ResizeMode=NoResize`, `ShowInTaskbar=False`, `Topmost`, `ShowActivated=False`, estilos estendidos `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, `SizeToContent=Width`, borda `BordaVidro`, raio pílula, vidro via `VidroIlha` com a técnica da Task 1); `ReservaTopo`; `ControleBarra` com ilhas ainda com conteúdo de placeholder ("Mídia", "Relógio", "Status"). Em `App.OnStartup`: montar `EstadoBarra(new ControleBarra(...), config)`, passar ao `Aplicador` e ao `ServicoRetratos` (`barraAtual: estado.Atual`) e, depois de `AoIniciar`, chamar `controle.Aplicar(Servico.BarraAtual())`. Em `Sair`, fechar ilhas e liberar a reserva.
- [ ] **Step 5: Verificação manual com o Gustavo** (pedir autorização para aplicar): criar um perfil só com `Barra do topo: Ligada`, Testar (20 s) → ilhas aparecem e uma janela maximizada começa abaixo delas; Voltar → somem e a reserva some. Screenshot de cada estado.
- [ ] **Step 6: Commit** `feat(app): reserva do topo e ilhas de vidro`.

---

### Task 7: Fontes reais e conteúdo das ilhas

**Files:**
- Create: `src/Vitral.App/Barra/FonteMidiaWindows.cs`, `FonteVolumeWindows.cs`, `FonteRedeWindows.cs`, `FonteBateriaWindows.cs`, `FonteDesempenhoWindows.cs`, `RelogioDoSistema.cs`; conteúdo XAML das ilhas em `src/Vitral.App/Barra/IlhaMidia.xaml`, `IlhaRelogio.xaml`, `IlhaStatus.xaml` (UserControls)
- Modify: `src/Vitral.App/Barra/ControleBarra.cs` (cria VMs com as fontes reais e as descarta ao fechar)

**Interfaces:**
- Consumes: todas as interfaces e VMs da Task 4; `LogBarra`.
- Produces: implementações que **nunca lançam** (try/catch → `LogBarra.Erro` → valor null) e disparam `Mudou` pelo `Dispatcher` da interface.
  - Mídia: `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`, sessão atual, `MediaPropertiesChanged`/`PlaybackInfoChanged`/`CurrentSessionChanged`; `NomeApp` pelo `SourceAppUserModelId`; capa via `Thumbnail` (opcional, null em falha). `TrazerApp` ativa a janela principal do processo do app quando encontrada.
  - Volume: `IMMDeviceEnumerator` → `IAudioEndpointVolume` (COM, `ComImport`), `OnNotify` via `IAudioEndpointVolumeCallback`.
  - Rede: `NetworkInformation.GetInternetConnectionProfile()` + `NetworkStatusChanged`.
  - Bateria: `PowerManager.BatteryStatus == NotPresent` → null; `RemainingChargePercent`, `PowerSupplyStatus`.
  - Desempenho: `GetSystemTimes` (delta entre leituras; primeira devolve null) e `GlobalMemoryStatusEx` (`dwMemoryLoad / 100`).
- Visual (spec §3.1): mídia = capa 18 px ou ícone, título, ⏮ ⏸/▶ ⏭ com o central em `Ambar`; relógio = hora semibold + data `TextoSecundario`; status = mini barras âmbar CPU/MEM com %, separador `Linha`, ícone de rede (glifos Segoe Fluent Icons), volume com número, bateria com %.

- [ ] **Step 1: Implementar as fontes** e o conteúdo; build com 0 avisos; suítes PASS.
- [ ] **Step 2: Verificação manual com o Gustavo** (barra ligada por Testar ou Aplicar, com autorização): música no Spotify e vídeo no navegador (título, ⏸, ⏭, a ilha some ao fechar); roda no volume (±2) e clique (mudo); desligar o Wi-Fi não derruba nada; CPU/MEM mexem. Conferir `barra.log` vazio ou só com erros esperados.
- [ ] **Step 3: Commit** `feat(app): dados reais nas ilhas da barra do topo`.

---

### Task 8: Comportamentos do sistema e cliques

**Files:**
- Create: `src/Vitral.App/Barra/Atalhos.cs`
- Modify: `src/Vitral.App/Barra/ControleBarra.cs`, `ReservaTopo.cs`, os UserControls das ilhas

**Interfaces:**
- Consumes: `ReservaTopo.TelaCheia`/`Recriar`, `PosicaoIlhas`.
- Produces: `static class Atalhos { void CentralDeNotificacoes(); void ConfiguracoesRapidas(); void GerenciadorDeTarefas(); }` — Win+N e Win+A por `SendInput`; `taskmgr.exe` por `Process.Start` com `UseShellExecute`.

- [ ] **Step 1: Implementar** conforme spec §4:
  - clique no relógio → `CentralDeNotificacoes`; em rede/bateria → `ConfiguracoesRapidas`; em CPU/MEM → `GerenciadorDeTarefas`; no título da mídia → `TrazerApp`;
  - `TelaCheia(true)` esconde as ilhas, `false` mostra;
  - `RegisterWindowMessage("TaskbarCreated")` → `Registrar` de novo e reposicionar;
  - `WM_DISPLAYCHANGE`, `WM_DPICHANGED`, `SystemEvents.DisplaySettingsChanged` → `Reposicionar` + `PosicaoIlhas.Calcular` no monitor principal atual;
  - `SystemEvents.PowerModeChanged(Resume)` → fontes releem tudo (`Mudou` em cada VM).
- [ ] **Step 2: Verificação manual com o Gustavo:** vídeo em tela cheia (somem e voltam); reiniciar o Explorer pelo Gerenciador de Tarefas (voltam sozinhas, reserva de volta); mudar a escala do monitor (se ele quiser) ou o monitor principal; clique no relógio e no Wi-Fi; clique em CPU.
- [ ] **Step 3: Commit** `feat(app): tela cheia, Explorer, monitores e atalhos da barra do topo`.

---

### Task 9: Fechamento e instalador 0.2.0

**Files:**
- Modify: `src/Vitral.App/Vitral.App.csproj` (`<Version>0.2.0</Version>`), `docs/instalar-no-notebook.md` (mencionar a barra do topo e que ela é ligada pelo perfil)

- [ ] **Step 1: Suítes completas** (`dotnet test -c Release`) → todas PASS; build sem avisos.
- [ ] **Step 2: Revisão final do ramo** por um agente Opus (foco: Review Focus acima, vazamento de handles/COM nas fontes, reserva liberada em todos os caminhos de saída, thread dos eventos). Corrigir Crítico/Importante com teste primeiro.
- [ ] **Step 3: Com autorização do Gustavo,** `pwsh -File build.ps1 -Versao 0.2.0`, instalar com `VitralApp-win-Setup.exe --silent` e abrir pelo `explorer.exe`; conferir a versão em `%LOCALAPPDATA%\VitralApp\current\Vitral.exe`.
- [ ] **Step 4: Commit** `chore: versão 0.2.0` e, com o ok do Gustavo, juntar na main.
