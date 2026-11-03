# Painel de controles: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bluetooth (e aparelhos), Modo avião, Não perturbe, Luz noturna e Brilho num painel que desce da ilha de
status, e o microfone no painel de volume.

**Architecture:** Regras puras no Core (`Vitral.Core/Controles`); o modelo do painel e o microfone na Ui (fontes por
interface, como as do volume); as fontes de verdade no App (`Vitral.App/Controles`), cada uma com o caminho provado na
prova dos controles.

**Tech Stack:** .NET 10 WPF, xUnit, WinRT (`Windows.Devices.Radios`, `Windows.Devices.Bluetooth`), COM (Core Audio,
`IKsControl`, `IQuietHoursSettings`), WMI, `dxva2`.

**Spec:** `docs/superpowers/specs/2026-11-03-controles-design.md` (prova: `docs/superpowers/specs/2026-11-02-prova-controles.md`)

## Global Constraints

- Testes: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet test --nologo -v q'`; feche o Vitral antes.
- Português no código e nos textos; commits terminam com `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Textos (exatos): rótulos "Bluetooth", "Modo avião", "Não perturbe", "Luz noturna", "Brilho", "Microfone";
  aviso "O teclado e o mouse Bluetooth desconectam enquanto estiver desligado"; luz noturna sem formato conhecido:
  "Abra a luz noturna nas Configurações do Windows"; aparelho "conectado"/"desconectado".
- IDs: `KSPROPSETID_BtAudio` `7FA06C40-B8F6-4C7E-8556-E8C33A12E54D` (0 conectar, 1 desconectar);
  `IQuietHoursSettings` CLSID `f53321fa-34f8-4b7f-b9a3-361877cb94cf`, IID `6BFF4732-81EC-4FFB-AE67-B6C1BC29631F`;
  perfis `Microsoft.QuietHoursProfile.PriorityOnly` (ligado) e `Microsoft.QuietHoursProfile.Unrestricted` (desligado).
- Mexer em estado do Windows só pelo clique do usuário; nunca ao abrir o Vitral.

## Review Focus

1. Vitral fecha com o Modo avião ligado → a lista guardada no config devolve os rádios quando o usuário desligar o modo (na próxima abertura o botão continua ligado) (Task 2, `Aviao_guardado_sobrevive_e_devolve`).
2. Usuário liga o Bluetooth pelo Win+A com o Modo avião do Vitral ligado → desligar o modo não derruba o que ele ligou depois (Task 2, `Desligar_aviao_nao_desliga_o_que_ja_esta_ligado`).
3. Luz noturna com blob de formato diferente (outra versão do Windows) → não grava nada e mostra o texto (Task 2, `Luz_noturna_formato_desconhecido`).
4. Brilho arrastado depressa → no máximo uma escrita a cada 100 ms por tela, sem travar a interface (Task 3, `Brilho_arrastado_junta_as_escritas`).
5. Monitor desconectado com o painel aberto → o deslizante dele some na releitura, sem erro (Task 3, `Tela_que_sumiu_sai_da_lista`).

---

### Task 1: Prova do fone com um aparelho ligado

**Files:** `spike/controles` (descartável); resultado em `docs/superpowers/specs/2026-11-02-prova-controles.md` e na spec §4.2.

- [ ] **Step 1:** Com um fone ou caixa ligado pelo Gustavo, rodar `ProvaControles fone-conectar <nome>` e `fone-desconectar <nome>`; registrar se `BluetoothDevice.ConnectionStatus` vira `Connected` e volta.
- [ ] **Step 2:** Registrar o resultado. **Funcionou** → spec §4.2 vale. **Não** → spec §4.2 passa a "o clique abre `ms-settings:bluetooth` para todos" e a Task 4 não faz o caminho do áudio.
- [ ] **Step 3:** Commit `spike: prova do fone Bluetooth`.

### Task 2: Regras dos controles (Core)

**Files:**
- Create: `src/Vitral.Core/Controles/RegrasControles.cs`, `src/Vitral.Core/Controles/Controles.cs`
- Modify: `src/Vitral.Core/Configuracao/ConfigApp.cs` (`IReadOnlyDictionary<string, bool>? RadiosAntesDoAviao = null`, chave = tipo do rádio)
- Test: `tests/Vitral.Core.Tests/ControlesTests.cs`

**Interfaces:**
- Produces:
  - `public enum TipoAparelho { Audio, Teclado, Mouse, Celular, Controle, Outro }`; `public sealed record AparelhoBluetooth(string Id, string Nome, TipoAparelho Tipo, bool Conectado, bool TemAudio)`.
  - `RegrasControles.TipoPelaClasse(int classeMaior, int classeMenor, bool le, string nome)` → `TipoAparelho` (classe maior 4 = áudio, 5 = periférico com menor teclado/mouse, 2 = celular; LE pelo nome "KB"/"keyboard"/"mouse"/"controller", senão `Outro`).
  - `RegrasControles.AvisarTeclado(IEnumerable<AparelhoBluetooth>)` → `bool` (algum teclado ou mouse conectado).
  - `RegrasControles.Aviao(IReadOnlyDictionary<string,bool> radiosAgora)` → o dicionário a guardar; `RegrasControles.DevolverAviao(IReadOnlyDictionary<string,bool> guardado, IReadOnlyDictionary<string,bool> agora)` → os rádios a ligar (só os que estavam ligados antes e estão desligados agora; nunca desliga nada).
  - `RegrasControles.LuzNoturna(byte[] blob)` → `bool?` (nulo = formato desconhecido: tamanho < 25 ou byte 18 ∉ {0x13, 0x15}); `RegrasControles.LuzNoturnaComo(byte[] blob, bool ligar)` → `byte[]?` (nulo se desconhecido; ligar/desligar conforme a spec §4.4; carimbo: incrementa o primeiro byte de 10..14 que não é 0xFF).
- [ ] **Step 1: Testes** `Tipo_pela_classe` (Theory: (4,_)→Audio, (5,0x10 teclado)→Teclado, (5,0x20 mouse)→Mouse, (2,_)→Celular, LE "BT5.0 KB"→Teclado, LE "8BitDo Ultimate 2C"→Outro... "controller"→Controle), `Aviso_so_com_teclado_ou_mouse_conectado`, `Aviao_guarda_o_estado_de_cada_radio`, `Aviao_guardado_sobrevive_e_devolve` (config gravado e relido devolve Bluetooth e Wi-Fi), `Desligar_aviao_nao_desliga_o_que_ja_esta_ligado`, `Luz_noturna_liga_e_desliga` (blob de exemplo da máquina: 18=0x13 → ligar → 18=0x15, +2 bytes na 23, carimbo avançou; desligar volta ao tamanho), `Luz_noturna_formato_desconhecido` (blob curto e byte 18 = 0x14 → nulo e `LuzNoturnaComo` nulo).
- [ ] **Step 2:** Ver falhar. **Step 3:** Implementar. **Step 4:** Suíte verde. **Step 5:** Commit `feat: regras dos controles`.

### Task 3: Modelo do painel e microfone (Ui)

**Files:**
- Create: `src/Vitral.Ui/Barra/PainelControlesViewModel.cs`
- Modify: `src/Vitral.Ui/Barra/Fontes.cs` (interfaces abaixo; `IFonteDispositivosAudio` ganha `Entradas`, `TornarEntradaPadrao`), `src/Vitral.Ui/Barra/PainelVolumeViewModel.cs` (microfone), `src/Vitral.Ui/Barra/CoordenadorPaineis.cs` (`Painel.Controles`), `src/Vitral.Ui/Barra/EstadoNotch.Notificacoes.cs` (`NaoPerturbe`: `Notificou` só põe na fila se falso)
- Test: `tests/Vitral.Ui.Tests/PainelControlesTests.cs`, `tests/Vitral.Ui.Tests/PainelVolumeTests.cs` (microfone), `tests/Vitral.Ui.Tests/NotchNotificacoesTests.cs` (não perturbe)

**Interfaces:**
- Consumes: Task 2.
- Produces:
  - `public interface IFonteControles { Task<EstadoControles> Ler(); Task<bool> Bluetooth(bool ligar); Task<bool> Aviao(bool ligar); bool NaoPerturbe(bool ligar); bool LuzNoturna(bool ligar); void Brilho(string telaId, int valor); Task<bool> Aparelho(AparelhoBluetooth a); }`
  - `public sealed record TelaBrilho(string Id, string Nome, int Valor)`; `public sealed record EstadoControles(bool? Bluetooth, bool Aviao, bool? NaoPerturbe, bool? LuzNoturna, IReadOnlyList<TelaBrilho> Telas, IReadOnlyList<AparelhoBluetooth> Aparelhos)` (nulo = indisponível).
  - `IFonteVolume` de entrada: `IFonteMicrofone { bool? Mudo { get; } event Action? Mudou; void AlternarMudo(); }`.
  - `PainelControlesViewModel(IFonteControles, Func<DateTime> agora)`: `Task Abrir()`, propriedades por controle (`Bluetooth`, `Aviao`, `NaoPerturbe`, `LuzNoturna`: `bool?`), `ObservableCollection<TelaBrilho> Telas`, `ObservableCollection<AparelhoBluetooth> Aparelhos`, `bool MostrarAparelhos`, `bool AvisoTeclado`, `string? TextoLuzNoturna`; comandos `AlternarBluetooth`, `AlternarAviao`, `AlternarNaoPerturbe`, `AlternarLuzNoturna`, `MostrarAparelhosCommand`, `Aparelho(AparelhoBluetooth)`; `void MudarBrilho(string telaId, int valor)` (junta escritas: no máximo uma a cada 100 ms por tela, a última sempre vai); evento `MudouNaoPerturbe(bool)`.
- [ ] **Step 1: Testes** (painel) `Abrir_le_o_estado`, `Interruptor_chama_a_fonte_e_rele`, `Indisponivel_fica_apagado`, `Luz_noturna_desconhecida_mostra_o_texto`, `Aviso_do_teclado`, `Brilho_arrastado_junta_as_escritas`, `Tela_que_sumiu_sai_da_lista`, `Nao_perturbe_avisa`; (volume) `Microfone_mudo_e_troca`; (notch) `Nao_perturbe_nao_desce_notificacao_mas_desce_o_Claude`.
- [ ] **Step 2:** Ver falhar. **Step 3:** Implementar. **Step 4:** Suíte verde. **Step 5:** Commit `feat: modelo do painel de controles e microfone`.

### Task 4: Fontes de verdade (App)

**Files:**
- Create: `src/Vitral.App/Controles/FonteControlesWindows.cs` (implementa `IFonteControles`), `src/Vitral.App/Controles/FonesBluetooth.cs` (o `IKsControl` da prova, se a Task 1 passou), `src/Vitral.App/Controles/NaoPerturbe.cs`, `src/Vitral.App/Controles/LuzNoturna.cs`, `src/Vitral.App/Controles/Brilho.cs`
- Modify: `src/Vitral.App/Barra/FonteDispositivosAudioWindows.cs` (entradas, `eCapture`), `src/Vitral.App/Barra/CoreAudio.cs` (o que faltar para captura), `src/Vitral.App/Barra/` a fonte de volume (microfone)

**Interfaces:** Consumes Tasks 2 e 3. Produces `FonteControlesWindows(ServicoConfig config)`.

- [ ] **Step 1:** Cada fonte pelo caminho da spec §4 (a prova tem o código de cada um em `spike/controles/Program.cs`); chamadas ao Windows fora da thread da interface; cada falha vira `false`/nulo com log, nunca exceção para o painel.
- [ ] **Step 2: Roteiro na máquina** (com um pequeno executável de teste, ou o Vitral de desenvolvimento chamando a fonte pelo atalho F9 da janela): ler o estado; Bluetooth e modo avião indo e voltando; não perturbe; luz noturna (o Gustavo confere a tela); brilho do notebook e do monitor externo; microfone mudo; fone (se a Task 1 passou). Build sem avisos.
- [ ] **Step 3:** Commit `feat: fontes dos controles`.

### Task 5: Painel e ícone na barra do topo

**Files:**
- Create: `src/Vitral.App/Barra/PainelControles.xaml(.cs)` (no molde do `PainelWifi`)
- Modify: `src/Vitral.App/Barra/IlhaStatus.xaml(.cs)` (ícone à esquerda do Wi-Fi, `Pediu?.Invoke(Painel.Controles)`), `src/Vitral.App/Barra/ControleBarra.cs` (criar o painel, alinhado à direita; `MudouNaoPerturbe` → `EstadoNotch.NaoPerturbe`), `src/Vitral.App/Barra/PainelVolume.xaml` (seção Microfone), `src/Vitral.App/App.xaml.cs` (fonte)

- [ ] **Step 1:** Painel conforme a spec §3 e os textos das Global Constraints. **Step 2:** Build sem avisos; suíte verde. **Step 3:** Commit `feat: painel de controles na barra do topo`.

### Task 6: Roteiro final e documentos

- [ ] **Step 1:** Com o Gustavo, o roteiro da spec §6. **Step 2:** Notas, `docs/ideias-pendentes.md` (tira "Completar os painéis"; "Já feito"); commit `docs: painel de controles`.
