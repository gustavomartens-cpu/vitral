# Barra de tarefas — Plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** O perfil ajusta botões, comportamento e ícones da barra de tarefas do Windows, e o relógio de baixo some com a barra do topo ligada, tudo reversível pelos retratos.

**Architecture:** O Core ganha o esquema 3 (`BarraTarefasPerfil`), a tradução das opções em valores de `HKCU` (`BarraDeTarefas`, `Icones`), `IRegistro.SubChaves`, um aviso novo `INotificador.AvisarOcultarAutomatico` e o estado da lista de ícones no `config.json`. O Aplicador grava tudo junto com os ajustes nativos; os retratos capturam os valores fixos e o `IsPromoted` de cada ícone. O App observa ícones novos e ganha a aba "Barra de tarefas".

**Tech Stack:** .NET 10, WPF, CommunityToolkit.Mvvm 8.4.2, xUnit; Win32 (`SHAppBarMessage`, `RegNotifyChangeKeyValue`). Nenhum pacote novo.

**Spec:** `docs/superpowers/specs/2026-10-25-barra-de-tarefas-design.md`.

## Global Constraints

- Ambiente: `. tools/env.ps1` antes de `dotnet`; nada fora de `G:\dev`; `TreatWarningsAsErrors`.
- Só `HKCU`, sem código no Explorer e sem elevação. O Vitral nunca reinicia o Explorer sozinho.
- Valores exatamente como a tabela §4 da spec (pesquisa 0/1/2; agrupar 0/1/2; `StuckRects3\Settings` byte 8, bit `0x01`; relógio `ShowSystrayDateTimeValueName` 0 ou apagado; `IsPromoted` 1/0).
- Ícones casados pelo nome do executável (`Path.GetFileName(ExecutablePath)`), sem diferenciar maiúsculas.
- Esquema do perfil: 3; esquemas 1 e 2 lidos com `barraTarefas = null`.
- Textos pt-BR da spec §6 ("Não mexer", "(não encontrado aqui)", rodapé "Transparência da barra: TranslucentTB. Com a barra do topo ligada, o relógio daqui some.", "Vale depois de reiniciar o Explorer").
- Mudanças reais no Windows e instalação só com o Gustavo autorizando na hora.

## Review Focus

1. **Perfil vindo do notebook com programas que não existem aqui** → os ausentes aparecem como "(não encontrado aqui)" e continuam na lista ao salvar; os presentes aplicam. Teste `Programa_do_perfil_que_nao_existe_continua_na_lista` na Task 5.
2. **Voltar/Desfazer depois de esconder ícones** → o `IsPromoted` de cada ícone volta (inclusive "ausente" vira apagado) e o observador para de esconder ícones novos. Testes na Task 4.
3. **`StuckRects3` ausente ou curto** → "ocultar automaticamente" vira não aplicado, sem exceção e sem tocar o resto. Teste na Task 3.
4. **Barra do topo desligada por um perfil que não fala da barra de tarefas** → o relógio volta (valor apagado). Teste na Task 3.
5. **Ícone novo aparecendo enquanto há "não mexer" aplicado** → nada muda. Teste na Task 4.

---

### Task 1: Teste do que vale na hora

Descartável: mudanças manuais no registro, desfeitas no fim. Só o resultado entra no plano.

**Files:**
- Modify: este plano (seção "Resultado do teste" ao fim da Task 1) e o ledger.

- [ ] **Step 1: Com autorização do Gustavo e ele na tela,** para cada opção:
  - **Valores comuns:** ler o valor atual, gravar o oposto, enviar `WM_SETTINGCHANGE "TraySettings"`, capturar a barra (`CopyFromScreen` da faixa de baixo), restaurar e capturar de novo. As opções são `ShowTaskViewButton`, `TaskbarSd`, `TaskbarGlomLevel`, `MMTaskbarEnabled`, `TaskbarBadges`, `TaskbarFlashing` e `SearchboxTaskbarMode` (0, 1, 2).
  - **Ocultar automaticamente:** `SHAppBarMessage(ABM_SETSTATE = 10)` com `lParam` 1 (oculta) ou 2 (sempre visível), na `APPBARDATA` da janela `Shell_TrayWnd`.
  - **Não reiniciar o Explorer neste teste.** A opção que não mudar na hora é marcada "exige reinício".
- [ ] **Step 2: Registrar** a tabela "opção → na hora / exige reinício" abaixo e no ledger; confirmar que tudo voltou ao estado inicial.
- [ ] **Step 3: Commit** `docs: o que vale na hora na barra de tarefas`.

**Resultado do teste:** _(preencher no Step 2)_

---

### Task 2: Esquema 3 do perfil

**Files:**
- Create: `src/Vitral.Core/Perfis/BarraTarefasPerfil.cs`
- Modify: `src/Vitral.Core/Perfis/Perfil.cs`, `src/Vitral.Core/Perfis/PerfilJson.cs`
- Test: `tests/Vitral.Core.Tests/BarraTarefasPerfilTests.cs` (+ ajustes dos testes que esperam esquema 2)

**Interfaces:**
- Produces:
  - `public enum PesquisaBarra { Oculta, Icone, Caixa }` e `public enum AgruparBotoes { Sempre, QuandoCheia, Nunca }`
  - `public sealed record BarraTarefasPerfil(PesquisaBarra? Pesquisa = null, bool? VisaoTarefas = null, bool? MostrarAreaDeTrabalho = null, AgruparBotoes? Agrupar = null, bool? OcultarAutomaticamente = null, bool? TodosOsMonitores = null, bool? Selos = null, bool? Piscar = null, IReadOnlyList<string>? IconesVisiveis = null)` com `Equals`/`GetHashCode` que comparam `IconesVisiveis` por sequência, sem diferenciar maiúsculas.
  - `Perfil(..., BarraTopoPerfil? BarraTopo = null, BarraTarefasPerfil? BarraTarefas = null)`; `PerfilJson.EsquemaSuportado = 3`.

- [ ] **Step 1: Testes que falham** em `BarraTarefasPerfilTests`:
  - `Esquema3_ida_e_volta`: o perfil com todos os campos (pesquisa `icone`, agrupar `quandoCheia`, ícones `["Spotify.exe"]`) → `Escrever` → `Ler` volta igual. O JSON contém `"esquema": 3`, `"pesquisa": "icone"` e `"agrupar": "quandoCheia"`.
  - `Esquemas_1_e_2_leem_barra_de_tarefas_nula`.
  - `Lista_vazia_e_diferente_de_ausente`: `"iconesVisiveis": []` → lista vazia não nula; campo ausente → null.
  - `Igualdade_compara_a_lista_por_conteudo`: duas instâncias com `["a.exe"]` e `["A.EXE"]` são iguais.
- [ ] **Step 2: Rodar** `dotnet test tests/Vitral.Core.Tests --filter BarraTarefasPerfilTests` → FAIL (tipos inexistentes).
- [ ] **Step 3: Implementar**; os testes antigos que citam "esquema 2" como atual ou "esquema 3" como futuro passam a usar 3 e 4.
- [ ] **Step 4: Rodar** suítes Core e Ui → PASS.
- [ ] **Step 5: Commit** `feat(core): perfil esquema 3 com barraTarefas`.

---

### Task 3: Valores da barra, relógio e ocultar automaticamente

**Files:**
- Create: `src/Vitral.Core/Nativo/BarraDeTarefas.cs`
- Modify: `src/Vitral.Core/Nativo/INotificador.cs`, `src/Vitral.Core/Nativo/NotificadorEmSegundoPlano.cs`, `src/Vitral.Core/Windows/NotificadorWindows.cs`, `src/Vitral.Core/Aplicacao/Aplicador.cs`, `src/Vitral.Core/Retratos/ServicoRetratos.cs` (captura), os notificadores falsos dos testes
- Test: `tests/Vitral.Core.Tests/BarraDeTarefasTests.cs`

**Interfaces:**
- Consumes: `BarraTarefasPerfil` (Task 2), `BarraTopoPerfil` (v1).
- Produces:
  - `public static class BarraDeTarefas`:
    - `IReadOnlyList<(string Chave, string Nome, TipoValor Tipo)> Controlados`: `SearchboxTaskbarMode`, os seis valores de `Advanced` da spec, `ShowSystrayDateTimeValueName` e `StuckRects3\Settings` (Binario).
    - `IReadOnlyList<ValorRegistro> Calcular(BarraTarefasPerfil b, byte[]? stuckRectsAtual)`.
    - `ValorRegistro? Relogio(BarraTopoPerfil? topo)`: `Visivel` → Dword 0; não visível → `Dado` null (apaga); topo null → null.
    - `bool? OcultarAutomatico(byte[]? settings)`: null se curto (< 9 bytes) ou ausente.
  - `INotificador.AvisarOcultarAutomatico(bool ligado)`; `NotificadorWindows` usa `SHAppBarMessage(10, APPBARDATA{hWnd = FindWindow("Shell_TrayWnd")}, lParam 1 ou 2)`.
  - `Aplicador.Aplicar`:
    - Soma aos nativos os valores de `p.BarraTarefas` (sem ícones; ícones na Task 4) e `Relogio(p.BarraTopo)`.
    - Se algum valor da barra foi gravado → `AvisarMudancaDaBarra`.
    - Se o `StuckRects3` foi gravado → `AvisarOcultarAutomatico(bit)`.
    - `StuckRects3` curto ou ausente com `OcultarAutomaticamente` pedido → entra em `NaoAplicados`, com o motivo "Configuração da barra não encontrada."
  - `Aplicador.Reverter`: se o retrato tem `StuckRects3` presente → `AvisarOcultarAutomatico(bit do retrato)`.
  - `ServicoRetratos.Tirar` captura `AjustesNativos.Controlados` + `BarraDeTarefas.Controlados`.

- [ ] **Step 1: Testes que falham** em `BarraDeTarefasTests` (registro em memória, notificador falso que guarda as chamadas):
  - `Cada_opcao_vira_o_valor_da_spec`: pesquisa Oculta/Ícone/Caixa → 0/1/2; agrupar Sempre/QuandoCheia/Nunca → 0/1/2; visão de tarefas, mostrar área de trabalho, todos os monitores, selos e piscar → 0/1 nos nomes da spec.
  - `Ocultar_automatico_muda_so_o_bit_do_byte_8`: `stuck` = 48 bytes com `[8] = 0x02` e o resto 0xAB → com true, `[8] = 0x03` e o resto igual; com false, `0x02`.
  - `Stuck_rects_curto_vira_nao_aplicado`: 4 bytes → `Resultado.NaoAplicados` contém `Settings`; os outros valores são gravados.
  - `Relogio_segue_a_barra_do_topo`: ligada → 0; desligada → apagado; null → nada.
  - `Perfil_que_desliga_a_barra_do_topo_devolve_o_relogio`: depois de aplicar ligada e então um perfil com `BarraTopo = Desligada` e `BarraTarefas = null`, o valor fica ausente.
  - `Avisos`: aplicar só a pesquisa → `AvisarMudancaDaBarra`; aplicar ocultar automático → `AvisarOcultarAutomatico(true)`; reverter → `AvisarOcultarAutomatico` com o bit antigo.
  - `Retrato_captura_os_valores_da_barra`.
- [ ] **Step 2: Rodar** `--filter BarraDeTarefasTests` → FAIL.
- [ ] **Step 3: Implementar** e ajustar os notificadores falsos existentes (compilação).
- [ ] **Step 4: Rodar** suítes → PASS.
- [ ] **Step 5: Commit** `feat(core): barra de tarefas, relógio e ocultar automaticamente`.

---

### Task 4: Ícones perto do relógio

**Files:**
- Create: `src/Vitral.Core/Nativo/Icones.cs`, `src/Vitral.Core/Barra/EstadoIcones.cs`, `src/Vitral.App/BarraDeTarefas/ObservadorIcones.cs`
- Modify: `src/Vitral.Core/Registro/IRegistro.cs`, `RegistroEmMemoria.cs`, `src/Vitral.Core/Windows/RegistroWindows.cs`, `src/Vitral.Core/Configuracao/ConfigApp.cs`, `src/Vitral.Core/Aplicacao/Aplicador.cs`, `src/Vitral.Core/Retratos/ServicoRetratos.cs`, `src/Vitral.App/App.xaml.cs`
- Test: `tests/Vitral.Core.Tests/IconesTests.cs`

**Interfaces:**
- Produces:
  - `IRegistro.SubChaves(string chave) → IReadOnlyList<string>` (nomes de um nível; chave inexistente → vazio).
  - `public sealed record ProgramaBandeja(string Executavel, string Nome);`
  - `public static class Icones`:
    - `const string Chave = @"Control Panel\NotifyIconSettings"`;
    - `IReadOnlyList<ProgramaBandeja> Programas(IRegistro r)`: um por executável; o nome é o `InitialTooltip` ou o executável sem `.exe`; em ordem alfabética.
    - `IReadOnlyList<ValorRegistro> Calcular(IRegistro r, IReadOnlyList<string> visiveis)`: `IsPromoted` 1/0 para cada subchave com `ExecutablePath`.
    - `IReadOnlyList<ValorRegistro> Retratar(IRegistro r)`: o `IsPromoted` atual de cada subchave, ausente incluído.
    - `ValorRegistro? ParaNovo(IRegistro r, string subchave, IReadOnlyList<string> visiveis)`: só se `IsPromoted` está ausente.
  - `ConfigApp(..., IReadOnlyList<string>? IconesVisiveis = null)`.
  - `EstadoIcones(ServicoConfig config)`: `IReadOnlyList<string>? Atual()` e `void Definir(IReadOnlyList<string>? lista)`.
  - `Aplicador(..., EstadoBarra? barra = null, EstadoIcones? icones = null)`:
    - `Aplicar` com `IconesVisiveis` não nulo grava `Icones.Calcular` e `icones?.Definir(lista)`.
    - `Reverter` faz `icones?.Definir(null)`: depois de voltar, o observador para.
  - O retrato inclui `Icones.Retratar`.
  - `ObservadorIcones(IRegistro r, EstadoIcones estado, INotificador notificador) : IDisposable`:
    - Uma thread espera `RegNotifyChangeKeyValue(HKCU\Control Panel\NotifyIconSettings, subárvore, REG_NOTIFY_CHANGE_NAME | LAST_SET)`.
    - A cada mudança, com `estado.Atual()` não nulo, grava `ParaNovo` para cada subchave e avisa a barra.
    - Erro → `LogBarra.Erro` e para.

- [ ] **Step 1: Testes que falham** em `IconesTests` (registro em memória com três subchaves: `Spotify.exe`, `steam.exe` e `wallpaper64.exe`, esta com `IsPromoted` ausente):
  - `SubChaves_lista_um_nivel` (inclui a chave inexistente → vazio).
  - `Programas_com_nome_da_dica_ou_do_executavel`.
  - `Calcular_promove_so_os_da_lista_sem_diferenciar_maiusculas`: `["SPOTIFY.EXE"]` → Spotify 1, Steam 0, Wallpaper 0.
  - `Lista_vazia_esconde_todos`.
  - `Retrato_e_reversao_dos_icones`: aplicar `["steam.exe"]` e reverter → os `IsPromoted` voltam ao que eram, inclusive o ausente, que fica apagado.
  - `Aplicar_guarda_a_lista_e_reverter_limpa`: `EstadoIcones.Atual()` igual a `["steam.exe"]`, depois null.
  - `Icone_novo_recebe_pela_lista`: `ParaNovo` numa subchave nova de `Discord.exe` sem `IsPromoted` → 0; com `["discord.exe"]` → 1; com `IsPromoted` presente → null.
  - `Nao_mexer_nao_observa`: com `EstadoIcones.Atual() == null`, o observador não grava nada. É testado pela função que ele chama, `AplicarNovos(IRegistro, EstadoIcones)`, exposta como `internal static` no Core e com `InternalsVisibleTo` para os testes.
- [ ] **Step 2: Rodar** `--filter IconesTests` → FAIL.
- [ ] **Step 3: Implementar.** No `App.OnStartup`:
  - montar `EstadoIcones` com o mesmo `ServicoConfig`;
  - passá-lo ao `Aplicador`;
  - criar o `ObservadorIcones` com o `RegistroWindows` e o notificador;
  - descartá-lo em `Sair`.
- [ ] **Step 4: Rodar** suítes → PASS.
- [ ] **Step 5: Commit** `feat: ícones perto do relógio no perfil`.

---

### Task 5: Aba "Barra de tarefas"

**Files:**
- Create: `src/Vitral.Ui/EditorBarraTarefasViewModel.cs`
- Modify: `src/Vitral.Ui/Opcoes.cs`, `src/Vitral.Ui/PrincipalViewModel.cs` (carregar/salvar/descartar; parâmetro novo opcional `Func<IReadOnlyList<ProgramaBandeja>>? programasBandeja = null`), `src/Vitral.App/JanelaPrincipal.xaml`, `src/Vitral.App/App.xaml.cs`
- Test: `tests/Vitral.Ui.Tests/EditorBarraTarefasTests.cs`

**Interfaces:**
- Produces:
  - `Opcoes.Pesquisas` (Não mexer / Oculta / Ícone / Caixa) e `Opcoes.Agrupamentos` (Não mexer / Sempre / Quando cheia / Nunca) com enums opcionais `PesquisaOpcional`, `AgruparOpcional`.
  - `public sealed partial class ItemIcone(string executavel, string nome, bool encontrado) : ObservableObject`, com `[ObservableProperty] bool Visivel`. `Nome` recebe o sufixo " (não encontrado aqui)" quando `!encontrado`.
  - `EditorBarraTarefasViewModel(Func<IReadOnlyList<ProgramaBandeja>> programas)`:
    - opções: `PesquisaOpcional Pesquisa`, `Escolha VisaoTarefas`, `Escolha MostrarAreaDeTrabalho`, `AgruparOpcional Agrupar`, `Escolha OcultarAutomaticamente`, `Escolha TodosOsMonitores`, `Escolha Selos`, `Escolha Piscar`;
    - ícones: `Escolha EscolherIcones` (Sim = "Escolher"), `ObservableCollection<ItemIcone> Icones`;
    - `void Carregar(BarraTarefasPerfil? b)` e `BarraTarefasPerfil? ParaPerfil()` (tudo "Não mexer" → null).
  - `PrincipalViewModel.EditorBarraTarefas`; o `SalvarEditor` monta o perfil com `BarraTarefas`.
  - Opções que a Task 1 marcou "exige reinício" ganham o texto "Vale depois de reiniciar o Explorer" no XAML.

- [ ] **Step 1: Testes que falham**:
  - `Carregar_e_devolver_ida_e_volta` (null, tudo preenchido).
  - `Tudo_nao_mexer_vira_nulo`.
  - `Escolher_icones_lista_os_programas_marcando_os_do_perfil`.
  - `Programa_do_perfil_que_nao_existe_continua_na_lista`: o perfil tem `["Notebook.exe"]` e os programas são `[Spotify]` → dois itens. "Notebook (não encontrado aqui)" vem marcado e salva de volta em `IconesVisiveis`.
  - `Salvar_grava_a_secao_no_perfil` (via `PrincipalViewModel`, arquivo com `esquema: 3`).
  - `Descartar_volta_a_secao_salva`.
- [ ] **Step 2: Rodar** `--filter EditorBarraTarefasTests` → FAIL.
- [ ] **Step 3: Implementar** o ViewModel e a ligação no `PrincipalViewModel`.
- [ ] **Step 4: Rodar** suítes → PASS.
- [ ] **Step 5: XAML.** A aba "Barra de tarefas" deixa de ser `IsEnabled="False"` e ganha três `CartaoVidro`: Botões, Comportamento e Ícones perto do relógio, este com `SeletorPilula` Não mexer/Escolher e uma lista de `CheckBox` com rolagem de altura máxima 220. Rodapé com o texto da spec. Opacidade 0.45 quando desabilitado, como nas outras abas. O App passa `() => Icones.Programas(registro)`.
- [ ] **Step 6: Verificação na tela com o Gustavo** (Testar de 20 s, com autorização): cada opção muda a barra e Voltar desfaz; ícones à mostra mudam; um app aberto depois do Testar vai para a setinha; com a barra do topo ligada, o relógio de baixo some.
- [ ] **Step 7: Commit** `feat(ui): aba Barra de tarefas`.

---

### Task 6: Fechamento e instalador 0.4.0

- [ ] **Step 1:** suítes completas (`dotnet test -c Release`) → PASS; build sem avisos.
- [ ] **Step 2:** revisão final do ramo por um agente Opus. Foco: o Review Focus acima, a thread do observador (fim limpo, sem gravar durante um Testar), as escritas parciais e os retratos com muitas subchaves de ícones. Corrigir Crítico/Importante com teste primeiro.
- [ ] **Step 3:** com autorização do Gustavo:
  - `<Version>0.4.0</Version>`;
  - `build.ps1 -Versao 0.4.0`;
  - instalar em silêncio e abrir pelo `explorer.exe`;
  - commit `chore: versão 0.4.0`;
  - com o ok dele, juntar na main.
