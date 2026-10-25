# Barra do topo v2 — Plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A ilha do relógio vira um notch preso à borda, e cinco painéis de vidro próprios (calendário, Wi-Fi, volume, bateria e energia, mídia expandida) descem das ilhas no lugar dos painéis do Windows.

**Architecture:** O Ui ganha um `CoordenadorPaineis` (um painel por vez), cinco ViewModels de painel e três fontes novas, todos testados com fontes falsas. O App ganha `JanelaPainel` (janela de vidro ativável sob a ilha), a variante notch da `JanelaIlha`, as fontes do Windows novas e o conteúdo XAML de cada painel. As ilhas avisam por evento qual painel pedir.

**Tech Stack:** .NET 10, WPF, CommunityToolkit.Mvvm 8.4.2, xUnit; WinRT (`Windows.Devices.WiFi`, `Windows.Devices.Radios`, `Windows.Devices.Geolocation`, `Windows.Media.Control`, `Windows.System.Power`); COM/Win32 (`IMMDeviceEnumerator`, `IPolicyConfig`, powrprof). Nenhum pacote NuGet novo.

**Spec:** `docs/superpowers/specs/2026-10-25-barra-do-topo-v2-design.md` (e a v1: `docs/superpowers/specs/2026-10-24-identidade-e-barra-do-topo-design.md`).

## Global Constraints

- Ambiente: `. tools/env.ps1` antes de `dotnet`; nada instalado fora de `G:\dev`. `TreatWarningsAsErrors` está ligado.
- Notch: janela 10 px acima da borda, 44 px de altura, 34 px visíveis; acrílico da v1 (`VidroIlha`).
- Painéis: 6 px abaixo da ilha; mídia alinha pela esquerda, calendário centralizado, Wi-Fi/volume/bateria pela direita; nunca a menos de 12 px da borda do monitor. Larguras: calendário 248, Wi-Fi/volume/bateria 280, mídia 260.
- Entrada: conteúdo desliza 8 px para baixo em 120 ms, sem fade.
- Um painel por vez; fecha com clique fora (perda de foco), Esc, novo clique na ilha, tela cheia, barra desligada ou Vitral saindo.
- Fontes: mesmo contrato da v1 — nunca lançam, falha vira null/"—"/controle escondido e vai para `logs\barra.log`; eventos na thread da interface.
- Textos em pt-BR, exatamente como na spec §3.3 e §6 ("Mais configurações de rede…", "Abrir o app", "Hoje", "Carregando", "Conectado", "Para ver redes por perto, permita a localização para apps da área de trabalho", "Abrir configurações").
- Esquema do perfil continua 2. Toda mudança real no Windows (aplicar perfil, conectar Wi-Fi, trocar saída, modo de energia) e toda instalação só com o Gustavo autorizando na hora.

## Review Focus

1. **Clicar de novo na ilha com o painel aberto fecha** (não reabre logo após fechar por perda de foco). Teste `Reabrir_logo_apos_perder_o_foco_nao_reabre` na Task 2.
2. **Arrastar o volume não briga com os avisos de volume que chegam** (sem eco nem tremida). Teste `Aviso_da_fonte_nao_volta_como_definir` na Task 3.
3. **Mídia com duração zero, posição além do fim ou pausada** → barra escondida, tempo limitado, estimativa parada. Testes na Task 5.
4. **Senha errada no Wi-Fi** → mensagem "Senha incorreta.", campo de senha continua aberto e limpo. Teste na Task 6.
5. **Painel aberto quando entra tela cheia ou a barra é desligada** → fecha. Teste `Fechar_todos` na Task 2 + verificação manual na Task 7.

---

### Task 1: Notch

**Files:**
- Modify: `src/Vitral.Ui/Barra/PosicaoIlhas.cs`, `src/Vitral.App/Barra/JanelaIlha.cs`, `src/Vitral.App/Barra/ControleBarra.cs`, `src/Vitral.App/Barra/IlhaRelogio.xaml`
- Test: `tests/Vitral.Ui.Tests/PosicaoIlhasTests.cs`

**Interfaces:**
- Produces: `PosicaoIlhas.NotchSobra = 10`, `PosicaoIlhas.NotchAltura = 44`; `Calcular(...)` devolve o relógio com `Y = monitor.Y - NotchSobra` e `Altura = NotchAltura` (mídia e status como antes). `JanelaIlha(string nome, bool presaNaBorda = false)`.

- [ ] **Step 1: Ajustar os testes** em `PosicaoIlhasTests`: `Monitor_na_origem` → relógio `(890, -10, 140, 44)`; `Monitor_com_origem_negativa` → relógio `(-610, -487, 140, 44)`. Mídia e status iguais.
- [ ] **Step 2: Rodar** `dotnet test tests/Vitral.Ui.Tests --filter PosicaoIlhasTests` → FAIL (relógio em Y 8).
- [ ] **Step 3: Implementar** as constantes e o relógio em `Calcular`. `JanelaIlha` com `presaNaBorda`: `Height = NotchAltura`, `Padding` da casca `12,NotchSobra,12,0` (conteúdo centralizado nos 34 px visíveis), raio da casca 9 só embaixo (`CornerRadius(0,0,9,9)`). `ControleBarra` cria o relógio com `presaNaBorda: true`. `IlhaRelogio.xaml` mantém hora + data.
- [ ] **Step 4: Rodar** suítes → PASS; build sem avisos.
- [ ] **Step 5: Verificação na tela com o Gustavo:** o notch encosta na borda, com desfoque e só os cantos de baixo arredondados à vista. Captura direta da faixa de cima (`CopyFromScreen`). Se o desfoque sumir ou aparecer fresta no topo, parar e registrar no ledger antes da Task 2.
- [ ] **Step 6: Commit** `feat(barra): relógio em forma de notch`.

---

### Task 2: Mecânica dos painéis e calendário

**Files:**
- Create: `src/Vitral.Ui/Barra/CoordenadorPaineis.cs`, `src/Vitral.Ui/Barra/CalendarioViewModel.cs`, `src/Vitral.App/Barra/JanelaPainel.cs`, `src/Vitral.App/Barra/PainelCalendario.xaml(.cs)`
- Modify: `src/Vitral.Ui/Barra/PosicaoIlhas.cs`, `src/Vitral.App/Barra/ControleBarra.cs`, `IlhaRelogio.xaml(.cs)`, `IlhaStatus.xaml(.cs)`, `IlhaMidia.xaml(.cs)`
- Test: `tests/Vitral.Ui.Tests/PaineisTests.cs`, `tests/Vitral.Ui.Tests/CalendarioTests.cs`, `tests/Vitral.Ui.Tests/PosicaoIlhasTests.cs`

**Interfaces:**
- Produces:
  - `public enum Painel { Nenhum, Calendario, Wifi, Volume, Bateria, Midia }`
  - `public sealed partial class CoordenadorPaineis : ObservableObject` com `Painel Aberto`, `void Alternar(Painel p, DateTime agora)`, `void FecharPorFoco(Painel p, DateTime agora)` (só fecha se `p` é o aberto; guarda `p` e `agora`), `void FecharTodos()`, `const int JanelaReaberturaMs = 300`. `Alternar` do mesmo painel fechado por foco há menos de 300 ms não reabre (o clique na ilha que tirou o foco).
  - `public enum AlinhamentoPainel { Esquerda, Centro, Direita }` e `PosicaoIlhas.Painel(Retangulo ilha, double largura, double altura, AlinhamentoPainel alinhamento, Retangulo monitor) → Retangulo` (Y = base da ilha + 6; X pelo alinhamento; limitado a 12 px das bordas).
  - `public sealed record DiaCalendario(int Numero, bool DoMes, bool Hoje);`
  - `CalendarioViewModel(IRelogioDoSistema agora)`: `string Titulo` ("outubro de 2026"), `IReadOnlyList<DiaCalendario> Dias` (42), `string DataPorExtenso` ("domingo, 25 de outubro de 2026"), comandos `Anterior`, `Proximo`, `Hoje`; `void Reabrir()` (volta ao mês atual e relê hoje).
  - Ilhas avisam com `public event Action<Painel>? Pediu;` nos três UserControls (relógio → Calendario; rede → Wifi; volume → Volume; bateria → Bateria; mídia fora dos botões → Midia). CPU/MEM continua no Gerenciador de Tarefas.
  - `JanelaPainel(string nome)`: janela de vidro (`VidroIlha.Preparar`), `ShowInTaskbar=False`, `Topmost`, `WS_EX_TOOLWINDOW` (sem `NOACTIVATE`), `ShowActivated=True`, `SizeToContent=Height`, `Width` fixa por painel; `event Action? PerdeuFoco` (Deactivated), Esc dispara `PediuFechar`; `void Mostrar(Retangulo posicao)` com o deslize de 8 px / 120 ms (`TranslateTransform` do conteúdo).

- [ ] **Step 1: Testes que falham**:
  - `PaineisTests`:
    - `Abrir_outro_fecha_o_anterior`: Alternar(Calendario) → Alternar(Volume) → `Aberto == Volume`.
    - `Alternar_o_mesmo_fecha`.
    - `Reabrir_logo_apos_perder_o_foco_nao_reabre`: aberto Volume; `FecharPorFoco(Volume, t)`; `Alternar(Volume, t + 100 ms)` → `Nenhum`; `Alternar(Volume, t + 400 ms)` → `Volume`.
    - `Perder_foco_de_painel_que_nao_esta_aberto_nao_fecha_o_atual`.
    - `Fechar_todos`.
  - `PosicaoIlhasTests`:
    - `Painel_da_direita_alinha_pela_ilha`: ilha `(1548,8,360,30)`, largura 280, monitor `(0,0,1920,1080)` → `(1628, 44, 280, alt)`.
    - `Painel_centralizado_no_notch`: ilha `(890,-10,140,44)`, largura 248 → `(836, 40, 248, alt)`.
    - `Painel_nao_sai_do_monitor`: ilha `(12,8,100,30)`, largura 260, alinhado à direita → X = 12.
  - `CalendarioTests` (agora = 25/10/2026 10:00):
    - `Outubro_de_2026_comeca_em_27_de_setembro`: `Dias[0] == new(27, false, false)`, `Dias.Count == 42`, `Dias.Single(d => d.Hoje).Numero == 25`.
    - `Titulo_e_data_por_extenso`: "outubro de 2026"; "domingo, 25 de outubro de 2026".
    - `Navega_e_atravessa_o_ano`: Proximo ×3 → "janeiro de 2027", sem `Hoje` marcado; Anterior → "dezembro de 2026".
    - `Hoje_volta_ao_mes_atual`.
- [ ] **Step 2: Rodar** `dotnet test tests/Vitral.Ui.Tests --filter "PaineisTests|CalendarioTests|PosicaoIlhasTests"` → FAIL (tipos inexistentes).
- [ ] **Step 3: Implementar** coordenador, posição e calendário. Grade: primeiro domingo ≤ dia 1 do mês, 42 dias. Textos: `CultureInfo("pt-BR")`, `"MMMM 'de' yyyy"` e `"dddd, d 'de' MMMM 'de' yyyy"`.
- [ ] **Step 4: Rodar** suítes → PASS.
- [ ] **Step 5: Implementar App:**
  - `JanelaPainel`.
  - `PainelCalendario.xaml`, como no mockup:
    - cabeçalho com ‹ ›;
    - linha "D S T Q Q S S";
    - `UniformGrid` 7×6, com hoje em `Ambar` e texto `TextoSobreAmbar`, e os dias de fora do mês com opacidade 0.3;
    - rodapé com a data por extenso e o link "Hoje".
  - `ControleBarra`:
    - cria um `CoordenadorPaineis` e assina o `Pediu` de cada ilha (`Alternar(p, DateTime.Now)`);
    - ao mudar `Aberto`, esconde a janela anterior e mostra a nova com `PosicaoIlhas.Painel` a partir da posição da ilha;
    - `PerdeuFoco` → `FecharPorFoco`;
    - tela cheia, `Fechar()` e `Aplicar` com barra desligada → `FecharTodos()`.
  - Na Task 2 só o calendário tem janela; os outros `Painel` ainda abrem o que abriam antes (Win+A).
- [ ] **Step 6: Verificação na tela:** clique no notch abre o calendário centralizado logo abaixo; ‹ › e "Hoje"; fecha com clique fora, Esc e novo clique no notch.
- [ ] **Step 7: Commit** `feat(barra): painéis próprios com o calendário do notch`.

---

### Task 3: Painel de volume

**Files:**
- Create: `src/Vitral.Ui/Barra/PainelVolumeViewModel.cs`, `src/Vitral.App/Barra/FonteDispositivosAudioWindows.cs`, `src/Vitral.App/Barra/PainelVolume.xaml(.cs)`
- Modify: `src/Vitral.Ui/Barra/Fontes.cs`, `src/Vitral.App/Barra/ControleBarra.cs`
- Test: `tests/Vitral.Ui.Tests/PainelVolumeTests.cs` (+ `DispositivosFalsos` em `FontesFalsas.cs`)

**Interfaces:**
- Consumes: `IFonteVolume` (v1), `CoordenadorPaineis`/`JanelaPainel` (Task 2).
- Produces:
  - `public sealed record DispositivoAudio(string Id, string Nome, bool Padrao);`
  - `public interface IFonteDispositivosAudio { IReadOnlyList<DispositivoAudio> Saidas { get; } bool PodeTrocar { get; } event Action? Mudou; void TornarPadrao(string id); }`
  - `PainelVolumeViewModel(IFonteVolume volume, IFonteDispositivosAudio dispositivos)`: `int Volume` (set chama `Definir`; atualização vinda da fonte não chama), `bool Mudo`, `bool Disponivel` (volume não nulo), comando `AlternarMudo`, `ObservableCollection<DispositivoAudio> Saidas`, `bool MostrarSaidas` (`PodeTrocar && Saidas.Count > 0`), comando `Escolher(DispositivoAudio)`; `IDisposable`.

- [ ] **Step 1: Testes que falham**:
  - `Mover_o_controle_define_o_volume`: `vm.Volume = 62` → `Definidos == [62]`.
  - `Aviso_da_fonte_nao_volta_como_definir`: a fonte muda para 40 e dispara `Mudou` → `vm.Volume == 40`, `Definidos` vazio.
  - `Lista_com_o_padrao_marcado` e `Escolher_troca_o_padrao` (`TornarPadrao("fone")`).
  - `Troca_indisponivel_esconde_a_lista`: `PodeTrocar = false` → `MostrarSaidas` false.
  - `Volume_indisponivel`: `Volume = null` → `Disponivel` false.
- [ ] **Step 2: Rodar** `--filter PainelVolumeTests` → FAIL.
- [ ] **Step 3: Implementar** o ViewModel.
- [ ] **Step 4: Rodar** suítes → PASS.
- [ ] **Step 5: Implementar App:**
  - `FonteDispositivosAudioWindows`, que assina `IMMNotificationClient` e relê em qualquer aviso:
    - Leitura: `EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE)` e `IMMDeviceCollection` (`GetCount`, `Item`).
    - Nome: `IMMDevice.OpenPropertyStore(STGM_READ)`, com `PKEY_Device_FriendlyName = {a45c254e-df1c-4efd-8020-67d146a850e0}, 14`.
    - Padrão: `GetDefaultAudioEndpoint(eRender, eMultimedia)`.
    - Troca: `IPolicyConfig.SetDefaultEndpoint(id, role)` para os papéis 0, 1 e 2. CLSID `PolicyConfigClient {870af99c-171d-4f9e-af0d-e63df40c2bc9}`, IID `IPolicyConfig {f8679f50-850a-41cf-9c72-430f290290c8}`. Ordem da vtable: GetMixFormat, GetDeviceFormat, ResetDeviceFormat, SetDeviceFormat, GetProcessingPeriod, SetProcessingPeriod, GetShareMode, SetShareMode, GetPropertyValue, SetPropertyValue, SetDefaultEndpoint, SetEndpointVisibility.
    - Falha ao criar o `PolicyConfigClient` → `PodeTrocar = false`.
  - `PainelVolume.xaml`, como no mockup:
    - número + "Mudo";
    - `Slider` 0–100 estilizado (trilho `Linha`, preenchimento `AmbarGradiente`, polegar `Texto`);
    - "SAÍDA" + lista com o padrão destacado em `Selecao`.
  - O clique no volume da ilha passa a `Pediu(Volume)`; o mudo sai da ilha e vai para o painel; a roda continua ±2.
- [ ] **Step 6: Verificação na tela (com autorização para trocar a saída):** arrastar o volume; trocar para o fone e de volta; mudo.
- [ ] **Step 7: Commit** `feat(barra): painel de volume com troca de saída`.

---

### Task 4: Painel de bateria e energia

**Files:**
- Create: `src/Vitral.Ui/Barra/PainelBateriaViewModel.cs`, `src/Vitral.App/Barra/FonteModoEnergiaWindows.cs`, `src/Vitral.App/Barra/PainelBateria.xaml(.cs)`
- Modify: `src/Vitral.Ui/Barra/Fontes.cs` (`EstadoBateria(int Porcentagem, bool Carregando, TimeSpan? TempoRestante = null)`), `src/Vitral.App/Barra/FontesSistema.cs`, `src/Vitral.App/Barra/ControleBarra.cs`
- Test: `tests/Vitral.Ui.Tests/PainelBateriaTests.cs`

**Interfaces:**
- Produces:
  - `public enum ModoEnergia { Economia, Equilibrado, Desempenho }`
  - `public interface IFonteModoEnergia { ModoEnergia? Atual { get; } event Action? Mudou; void Definir(ModoEnergia modo); void Reler(); }` (null = indisponível)
  - `PainelBateriaViewModel(IFonteBateria bateria, IFonteModoEnergia modo)`: `string Porcentagem` ("81%"), `double Fracao`, `string Detalhe`, `ModoEnergia? Modo`, `bool MostrarModo`, comando `DefinirModo(ModoEnergia)`; `IDisposable`. `Detalhe`:
    - `TempoRestante` → "3 h 20 min" / "45 min" / "3 h";
    - carregando abaixo de 100% → "Carregando";
    - carregando em 100% → "Conectado";
    - sem tempo e sem carregar → "Usando a bateria".

- [ ] **Step 1: Testes que falham**:
  - `Tempo_restante_formatado`: 3h20 → "3 h 20 min"; 45 min → "45 min"; 3h → "3 h".
  - `Carregando_e_conectado`.
  - `Tempo_desconhecido_mostra_usando_a_bateria`.
  - `Modo_atual_marcado_e_definir_chega_na_fonte`.
  - `Modo_indisponivel_esconde_o_seletor`.
- [ ] **Step 2: Rodar** `--filter PainelBateriaTests` → FAIL.
- [ ] **Step 3: Implementar** o ViewModel e o novo campo de `EstadoBateria`.
- [ ] **Step 4: Rodar** suítes → PASS.
- [ ] **Step 5: Implementar App:**
  - `FonteBateriaWindows` preenche `TempoRestante` com `PowerManager.RemainingDischargeTime`. Fica null se estiver carregando, se o valor for ≤ 0 ou se for `TimeSpan.MaxValue`.
  - `FonteModoEnergiaWindows`:
    - `PowerGetEffectiveOverlayScheme(out Guid)` e `PowerSetActiveOverlayScheme(Guid)` (powrprof);
    - Economia `961cc777-2547-4f9d-8174-7d86181b8a7a`, Equilibrado `Guid.Empty`, Desempenho `ded574b5-45a0-4f42-8737-46345c09c238`;
    - GUID desconhecido ou erro → `Atual = null`;
    - `Reler` ao abrir o painel e depois de `Definir`.
  - `PainelBateria.xaml`, como no mockup:
    - "81%" grande e o detalhe;
    - barra âmbar;
    - "MODO DE ENERGIA" com três pílulas (`SeletorPilula`).
- [ ] **Step 6: Verificação na tela** (no PC, que tem bateria; trocar o modo só com autorização).
- [ ] **Step 7: Commit** `feat(barra): painel de bateria e modo de energia`.

---

### Task 5: Mídia expandida

**Files:**
- Create: `src/Vitral.Ui/Barra/PainelMidiaViewModel.cs`, `src/Vitral.App/Barra/PainelMidia.xaml(.cs)`
- Modify: `src/Vitral.Ui/Barra/Fontes.cs` (`EstadoMidia(..., LinhaDoTempo? Tempo = null)`), `src/Vitral.App/Barra/FonteMidiaWindows.cs`, `src/Vitral.App/Barra/ControleBarra.cs`
- Test: `tests/Vitral.Ui.Tests/PainelMidiaTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record LinhaDoTempo(TimeSpan Posicao, TimeSpan Duracao, DateTime LidaEm);`
  - `PainelMidiaViewModel(IFonteMidia midia, IRelogio relogio, IRelogioDoSistema agora)`:
    - Textos: `string Titulo` (completo), `string Subtitulo` ("The Weeknd · Spotify"; sem artista → só o app).
    - Estado: `byte[]? Capa`, `bool Tocando`.
    - Progresso: `bool MostrarProgresso` (duração > 0), `double Fracao`, `string Decorrido`, `string Total` (`m:ss`; a partir de 1 h, `h:mm:ss`).
    - Comandos: `Anterior`, `TocarPausar`, `Proxima`, `AbrirApp` (→ `TrazerApp`).
    - `IDisposable`.
  - Posição estimada:
    - tocando → `Posicao + (Agora − LidaEm)`;
    - pausado → `Posicao`;
    - sempre limitada a `[0, Duracao]`.

- [ ] **Step 1: Testes que falham**:
  - `Progresso_e_tempos`: posição 1:16, duração 3:20, pausado → `Decorrido == "1:16"`, `Total == "3:20"`, `Fracao ≈ 0.38`.
  - `Tique_avanca_enquanto_toca`: tocando, agora +10 s e tique → "1:26".
  - `Pausado_nao_avanca`.
  - `Posicao_alem_do_fim_fica_no_fim`.
  - `Sem_duracao_esconde_o_progresso`.
  - `Subtitulo_sem_artista`.
  - `Abrir_app_chega_na_fonte`.
- [ ] **Step 2: Rodar** `--filter PainelMidiaTests` → FAIL.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar** suítes → PASS.
- [ ] **Step 5: Implementar App:**
  - `FonteMidiaWindows` lê `GetTimelineProperties()`, com `LidaEm` = `LastUpdatedTime` em hora local, e assina `TimelinePropertiesChanged`.
  - `PainelMidia.xaml`, como no mockup:
    - capa de 120 px de altura (`BytesParaImagem` com `DecodePixelWidth` 260), ou o ícone de música;
    - título e subtítulo;
    - progresso, com os tempos;
    - ⏮ ⏸ ⏭;
    - link "Abrir o app".
  - O clique no título da ilha passa a `Pediu(Midia)`.
- [ ] **Step 6: Verificação na tela:** música no Spotify, com o progresso andando, pausar e pular.
- [ ] **Step 7: Commit** `feat(barra): mídia expandida`.

---

### Task 6: Painel de Wi-Fi

**Files:**
- Create: `src/Vitral.Ui/Barra/PainelWifiViewModel.cs`, `src/Vitral.App/Barra/FonteWifiWindows.cs`, `src/Vitral.App/Barra/PainelWifi.xaml(.cs)`
- Modify: `src/Vitral.Ui/Barra/Fontes.cs`, `src/Vitral.App/Barra/ControleBarra.cs`
- Test: `tests/Vitral.Ui.Tests/PainelWifiTests.cs` (+ `WifiFalso`)

**Interfaces:**
- Produces:
  - `public sealed record RedeWifi(string Nome, int Sinal, bool Protegida, bool Salva, bool Conectada, bool Corporativa);` (Sinal 0–4)
  - `public sealed record EstadoWifi(bool TemAdaptador, bool RadioLigado, bool PermissaoLocalizacao, IReadOnlyList<RedeWifi> Redes);`
  - `public sealed record ResultadoConexao(bool Ok, string? Mensagem);`
  - `public interface IFonteWifi { EstadoWifi? Atual { get; } event Action? Mudou; Task AtualizarAsync(); Task<ResultadoConexao> ConectarAsync(string rede, string? senha); void Desconectar(); Task LigarAsync(bool ligado); void AbrirConfiguracoes(string uri); }`
  - `PainelWifiViewModel(IFonteWifi wifi)`:
    - Estado: `bool TemAdaptador`, `bool Ligado` (set → `LigarAsync`), `bool AvisoLocalizacao`, `string? Mensagem`, `bool Ocupado`.
    - Redes: `ObservableCollection<RedeWifi> Redes`, ordenadas: conectada primeiro, depois `Sinal` decrescente, depois nome.
    - Senha: `RedeWifi? PedindoSenha`, `string Senha`.
    - Comandos:
      - `Escolher(RedeWifi)`: conectada → desconecta; corporativa → `AbrirConfiguracoes("ms-settings:network-wifi")`; salva ou aberta → conecta; protegida nova → `PedindoSenha`.
      - `ConectarComSenha`.
      - `AbrirMais` (`ms-settings:network-wifi`).
      - `AbrirLocalizacao` (`ms-settings:privacy-location`).
    - `Task AbrirAsync()`: chama `AtualizarAsync` ao abrir o painel.

- [ ] **Step 1: Testes que falham**:
  - `Ordem_conectada_e_sinal`.
  - `Rede_protegida_nova_pede_senha`.
  - `Rede_salva_conecta_sem_senha`: `Conexoes == [("Casa", null)]`.
  - `Senha_errada_mostra_mensagem_e_mantem_o_campo`: a fonte responde `new(false, "Senha incorreta.")` → `Mensagem == "Senha incorreta."`, `PedindoSenha` continua a rede e `Senha == ""`.
  - `Conectada_desconecta`.
  - `Corporativa_abre_configuracoes`.
  - `Sem_permissao_de_localizacao_mostra_aviso`.
  - `Chave_liga_e_desliga`.
  - `Sem_adaptador`: `TemAdaptador` false e `Redes` vazia.
- [ ] **Step 2: Rodar** `--filter PainelWifiTests` → FAIL.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: Rodar** suítes → PASS.
- [ ] **Step 5: Implementar App:** `FonteWifiWindows`.
  - Adaptador:
    - `WiFiAdapter.RequestAccessAsync()`, depois `FindAllAdaptersAsync()`; sem adaptador → `TemAdaptador=false`.
    - Permissão de localização: `Geolocator.RequestAccessAsync()` diferente de `Allowed` → `PermissaoLocalizacao=false`.
  - Leitura das redes:
    - `ScanAsync`, depois `NetworkReport.AvailableNetworks`, agrupando pelo SSID e ficando com o melhor sinal.
    - Protegida: autenticação diferente de `Open80211`/`None`. Corporativa: `Rsna` ou `Wpa`.
    - Conectada: `NetworkAdapter.GetConnectedProfileAsync().ProfileName`.
    - Salva: nomes de `NetworkInformation.GetConnectionProfiles()` com `IsWlanConnectionProfile`.
  - Conectar:
    - `ConnectAsync(rede, Automatic)`, ou com `PasswordCredential { Password = senha }` para rede nova.
    - Mensagens de `WiFiConnectionStatus`:
      - `InvalidCredential` → "Senha incorreta.";
      - `NetworkNotAvailable` → "A rede não está ao alcance.";
      - `Timeout` → "Tempo esgotado.";
      - o resto → "Não foi possível conectar.".
  - Rádio: `Radio.RequestAccessAsync()` e o `Radio` de tipo `WiFi`, com `SetStateAsync`.
  - Configurações: `Process.Start` com `UseShellExecute` para as URIs.
  - `PainelWifi.xaml`, como no mockup:
    - "WI-FI" + chave (estilo novo `ChaveVidro`, âmbar quando ligada);
    - lista com ícone de sinal, cadeado e "Conectado" em âmbar;
    - campo de senha (`PasswordBox` com estilo de `CaixaVidro`) + "Conectar" (`BotaoPrimario`) logo abaixo da rede escolhida;
    - linha de mensagem;
    - aviso de localização com "Abrir configurações";
    - rodapé "Mais configurações de rede…".
- [ ] **Step 6: Verificação na tela (com autorização para desconectar/conectar):** a lista aparece; desconectar e reconectar na rede salva; o aviso de localização se ela estiver negada.
- [ ] **Step 7: Commit** `feat(barra): painel de Wi-Fi`.

---

### Task 7: Fechamento e instalador 0.3.0

**Files:**
- Modify: `src/Vitral.App/Vitral.App.csproj` (`<Version>0.3.0</Version>`), `docs/instalar-no-notebook.md` (uma frase sobre os painéis)

- [ ] **Step 1: Verificação geral na tela:**
  - um painel por vez;
  - um vídeo em tela cheia fecha o painel aberto;
  - desligar a barra fecha tudo;
  - conferir o `barra.log`.
- [ ] **Step 2: Suítes completas** (`dotnet test -c Release`) → PASS; build sem avisos.
- [ ] **Step 3: Revisão final do ramo** por um agente Opus: o Review Focus acima, liberação de COM/WinRT nas fontes novas, foco e ativação das janelas de painel, e as duas interfaces não documentadas falhando com elegância. Corrigir Crítico/Importante com teste primeiro.
- [ ] **Step 4: Com autorização do Gustavo:**
  - gerar com `pwsh -File build.ps1 -Versao 0.3.0`;
  - instalar com `--silent`;
  - abrir pelo `explorer.exe`.
- [ ] **Step 5: Commit** `chore: versão 0.3.0` e, com o ok do Gustavo, juntar na main.
