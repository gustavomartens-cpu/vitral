# Fase 0: prova de viabilidade (plano de implementação)

> **Para agentes:** sub-skill obrigatória: use superpowers:subagent-driven-development (recomendado) ou superpowers:executing-plans para executar este plano tarefa por tarefa. Os passos usam checkbox (`- [ ]`) para acompanhamento.

**Objetivo:** provar no PC do Gustavo (Windows 11 25H2, build 26200.9550) que uma DLL nossa, carregada via XAML Diagnostics, deixa a barra de tarefas transparente. Também medir as rotas de carga externa e interna, testar a entrada no Iniciar e testar se o UCPD bloqueia o nosso exe. O resultado é uma decisão documentada: seguir ou parar, e qual rota usar.

**Arquitetura:** código **descartável** em `spike/`. Tem quatro peças:
- uma DLL C++ mínima (`VitralTapSpike.dll`) que implementa `IObjectWithSite`, observa a árvore visual, registra tudo em log e troca o `Fill` de `BackgroundFill`;
- um console C# (`Injector`) que carrega a DLL por uma das duas rotas;
- um console C# (`UcpdProbe`) para o teste do UCPD;
- um relatório final.

**Stack:** .NET 10 SDK (console, P/Invoke), llvm-mingw 20260922 (clang++, C++23), C++/WinRT 2.0.250303.1, PowerShell 7.

**Spec:** `docs/superpowers/specs/2026-10-03-vitral-design.md` (§2 ambiente, §3.2 e §3.3 TAP e rotas, §8 ferramentas, §9 linha "Fase 0", §10 riscos).

## Restrições globais

- Ferramentas e caches em `G:\dev`. Nada novo no C:, exceto logs em `%LOCALAPPDATA%\Vitral\spike\`.
- Sem admin. Sem alterar o PATH nem variáveis de usuário de forma permanente: `spike/tools/env.ps1` vale só para a sessão do terminal.
- Antes de cada download, informe ao Gustavo o nome, a origem e o tamanho, e espere o OK. Origens permitidas: `dotnet.microsoft.com` / `builds.dotnet.microsoft.com`, `github.com/mstorsjo/llvm-mingw/releases`, `www.nuget.org` (Microsoft.Windows.CppWinRT 2.0.250303.1).
- Fechar o TranslucentTB, ou tirá-lo da inicialização, **só com confirmação do Gustavo**. Ao terminar a fase, oferecer reativá-lo.
- Nenhuma escrita em HKLM. Nenhuma escrita em HKCU, exceto no teste do UCPD (Tarefa 5), que regrava o **mesmo** valor que já existe.
- Flags da DLL, exatamente como na spec §8: `-std=c++23 -O2 -shared -mguard=cf-nochecks -ffp-exception-behavior=maytrap -DUNICODE -D_UNICODE -D__USE_MINGW_ANSI_STDIO=0`, mais `-static` (libc++ e libunwind estáticos são obrigatórios).
- CLSID fixo da TAP: `{6B1C2E52-8F0A-4C3B-9A5E-3D7F1E2A9C41}`.
- Pasta da DLL (`G:\dev\vitral\spike\out\`) com ACL de leitura e execução para `ALL APPLICATION PACKAGES` (`*S-1-15-2-1`).
- Todo o código de `spike/` é descartável. Nada daqui é copiado para a fase 1 sem ser reescrito com testes.

## Foco de revisão

1. **Explorer reiniciado com a DLL carregada:** precisa subir limpo, com a barra normal e sem ciclo de quedas. Coberto na Tarefa 4, passo "reinício".
2. **Três monitores:** cada barra secundária é outro `TaskbarFrame`, e as três precisam ficar transparentes. Coberto na Tarefa 3 (o log conta frames) e na Tarefa 4 (verificação visual).
3. **Injector morto à força:** a DLL precisa restaurar o `Fill` original. Coberto na Tarefa 3 (`RegisterWaitForSingleObject` no PID host) e na Tarefa 4.
4. **Rodar o injector duas vezes no mesmo Explorer:** o segundo precisa recusar (anexar uma vez por PID). Coberto na Tarefa 3 (mutex `Local\VitralTap-<pid>`) e na Tarefa 4.
5. **TranslucentTB ou Windhawk ainda rodando:** o injector precisa abortar com mensagem, porque só existe um consumidor de XAML Diagnostics por processo. Coberto na Tarefa 4 (checagem de processo).

---

### Tarefa 1: ferramentas em G:\dev

**Arquivos:**
- Criar: `spike/tools/instalar-ferramentas.ps1`, `spike/tools/env.ps1`, `spike/README.md`

**Interfaces:**
- Produz: depois de `. spike/tools/env.ps1`, a sessão tem `dotnet` (10.0.x), `clang++` e `llvm-objdump` (llvm-mingw) e `cppwinrt` no PATH, mais `NUGET_PACKAGES=G:\dev\nuget\packages`, `NUGET_HTTP_CACHE_PATH=G:\dev\nuget\http`, `DOTNET_CLI_HOME=G:\dev\dotnet-home` e `DOTNET_CLI_TELEMETRY_OPTOUT=1`.

- [ ] **Passo 1:** escrever `instalar-ferramentas.ps1`. Ele é idempotente: pula o que já existe.
  - .NET via `dotnet-install.ps1 -Channel 10.0 -Quality GA -InstallDir G:\dev\dotnet -NoPath`;
  - llvm-mingw `llvm-mingw-20260922-ucrt-x86_64.zip`, extraído em `G:\dev\toolchains\`;
  - `microsoft.windows.cppwinrt.2.0.250303.1.nupkg`, extraído em `G:\dev\toolchains\cppwinrt\`.
  - Cada download imprime a URL e o tamanho e pede `Read-Host` de confirmação. O Gustavo roda o script no terminal dele.
- [ ] **Passo 2:** escrever `env.ps1`, que prefixa o PATH só do processo atual.
- [ ] **Passo 3:** o Gustavo roda `pwsh spike/tools/instalar-ferramentas.ps1`.
- [ ] **Passo 4: verificar**
  Rodar: `. spike/tools/env.ps1; dotnet --version; clang++ --version; cppwinrt -?`
  Esperado: `10.0.` no início da versão do dotnet; `clang version 2` (LLVM 20 ou mais novo); a ajuda do cppwinrt aparece. Rodar também `Get-PSDrive C` antes e depois: o espaço livre não pode cair mais de 50 MB.
- [ ] **Passo 5:** `README.md` com o propósito do spike, o aviso de "descartável" e a ordem de execução das tarefas. Commit: `chore(spike): ferramentas da fase 0 em G:\dev`.

### Tarefa 2: projeção C++/WinRT e "hello" compilável

**Arquivos:**
- Criar: `spike/tap/gerar-projecao.ps1`, `spike/tap/hello.cpp`, `spike/tap/build.ps1`
- Gerado (ignorar no git): `spike/tap/generated/`, `spike/out/`

**Interfaces:**
- Produz: `spike/tap/build.ps1 -Fonte <arquivo.cpp> -Saida <arquivo.dll>`, que compila com as flags das restrições globais e `-I generated`, linka `-lole32 -loleaut32 -lruntimeobject -lshlwapi -luser32 -ladvapi32` e falha se `llvm-objdump -p` mostrar `libc++.dll` ou `libunwind.dll` nas importações.

- [ ] **Passo 1:** `gerar-projecao.ps1` roda `cppwinrt -input local -output spike/tap/generated`, usando os `.winmd` de `C:\Windows\System32\WinMetadata`. Acrescentar `spike/tap/generated/` e `spike/out/` ao `.gitignore`.
- [ ] **Passo 2:** `hello.cpp` é uma DLL com `DllMain` vazio que inclui `<winrt/Windows.UI.Xaml.Shapes.h>`, `<winrt/Windows.UI.Xaml.Media.h>` e `<xamlom.h>` e instancia `winrt::Windows::UI::Color{}`.
- [ ] **Passo 3: verificar**
  Rodar: `pwsh spike/tap/build.ps1 -Fonte spike/tap/hello.cpp -Saida spike/out/hello.dll`
  Esperado: compila; o script imprime `importações OK (sem libc++/libunwind)`. Se o clang falhar em headers do C++/WinRT, registrar o erro em `spike/resultados/notas.md` e seguir o plano B da spec §8 (MSVC portátil) antes de continuar.
- [ ] **Passo 4:** commit: `chore(spike): projeção C++/WinRT e build da DLL`.

### Tarefa 3: VitralTapSpike.dll

**Arquivos:**
- Criar: `spike/tap/VitralTapSpike.cpp`, `spike/tap/VitralTapSpike.def`

**Interfaces:**
- Consome: `build.ps1` (Tarefa 2).
- Produz:
  - DLL com as exportações `DllGetClassObject`, `DllCanUnloadNow` e `VitralHookProc` (assinatura `LRESULT CALLBACK VitralHookProc(int code, WPARAM wParam, LPARAM lParam)`), e a classe COM `TapSite` no CLSID fixo.
  - Dados de inicialização (string recebida por `IXamlDiagnostics::GetInitializationData`, ou lida de `%LOCALAPPDATA%\Vitral\spike\init.txt` na rota interna): `hostpid=<n>;modo=<transparente|log>`.
  - Log: `%LOCALAPPDATA%\Vitral\spike\tap-<pid>.log`, uma linha por evento, com a hora em milissegundos.
  - Mutex `Local\VitralTap-<pid do processo alvo>`, criado pela DLL ao anexar.
  - Evento `Local\VitralTapReady-<pid>`, sinalizado quando `SetSite` termina.

- [ ] **Passo 1: implementar `TapSite::SetSite(IUnknown*)`.** Ele:
  - obtém `IXamlDiagnostics` e lê os dados de inicialização;
  - se o mutex já existir, registra `JA_ANEXADO` e retorna `S_OK` sem fazer nada;
  - senão, cria o mutex, obtém `IVisualTreeService3` e chama `AdviseVisualTreeChange(watcher)` numa `std::thread` separada, com `join` (padrão do TranslucentTB);
  - sinaliza o evento Ready.
- [ ] **Passo 2: implementar `VisualTreeWatcher::OnVisualTreeChange`.** Ele:
  - registra no log `tipo|nome|handle` de cada elemento adicionado;
  - no modo `transparente`, para cada `Windows.UI.Xaml.Shapes.Rectangle` com nome `BackgroundFill` que tenha um ancestral `FrameworkElement` chamado `TaskbarFrame`: guarda o `Fill` original num mapa por handle e aplica um `SolidColorBrush` de cor `{A=0,R=0,G=0,B=0}`;
  - com nome `BackgroundStroke` no mesmo ancestral: aplica `Opacity = 0` e guarda o valor original;
  - conta e registra `FRAMES=<n>` a cada novo `Taskbar.TaskbarFrame`.
- [ ] **Passo 3: restauração.** `RegisterWaitForSingleObject` no `hostpid`: quando o host morre, despachar ao dispatcher de cada elemento a restauração dos valores guardados e registrar `RESTAURADO=<n>`.
- [ ] **Passo 4: rota interna.**
  - `VitralHookProc`: na primeira chamada (guardada por `std::atomic_flag`), fixa o módulo (`GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_PIN)`) e cria uma thread que tenta `InitializeXamlDiagnosticsEx(L"VisualDiagConnection<n>", GetCurrentProcessId(), nullptr, <caminho da própria DLL>, CLSID_TapSite, <dados de init.txt>)` para n = 1..60, com 500 ms entre tentativas, cada uma numa `std::thread` descartável;
  - sempre retorna `CallNextHookEx`.
- [ ] **Passo 5: build e verificação estática**
  Rodar: `pwsh spike/tap/build.ps1 -Fonte spike/tap/VitralTapSpike.cpp -Saida spike/out/VitralTapSpike.dll; llvm-objdump -p spike/out/VitralTapSpike.dll | Select-String 'DllGetClassObject|DllCanUnloadNow|VitralHookProc|DLL Name'`
  Esperado: as 3 exportações aparecem; as DLLs importadas são só do sistema (nenhuma `libc++`/`libunwind`).
- [ ] **Passo 6:** commit: `feat(spike): TAP mínima com log, transparência e restauração`.

### Tarefa 4: Injector e experimento das rotas

**Arquivos:**
- Criar: `spike/injector/Injector.csproj` (`net10.0-windows`, console, `AllowUnsafeBlocks`), `spike/injector/Program.cs`, `spike/injector/XamlDiag.cs`, `spike/injector/RotaInterna.cs`, `spike/resultados/rotas.csv`
- Criar: `spike/tools/preparar-pasta.ps1`, que aplica `icacls G:\dev\vitral\spike\out /grant "*S-1-15-2-1:(OI)(CI)(RX)"`

**Interfaces:**
- Consome: a DLL, o CLSID, os nomes de mutex e evento e o formato de init da Tarefa 3.
- Produz: `Injector --rota <externa|interna> --alvo <explorer|iniciar> --modo <transparente|log> [--manter]`. O código de saída é `0` (Ready recebido em até 35 s), `2` (já anexado), `3` (outro consumidor rodando) ou `4` (timeout). Cada execução acrescenta uma linha `data;rota;alvo;pid;resultado;ms` em `spike/resultados/rotas.csv`. Com `--manter`, o processo fica vivo como host até Enter (para testar a restauração).

- [ ] **Passo 1: `XamlDiag.cs`.**
  - `[DllImport("Windows.UI.Xaml.dll", CharSet=Unicode)] int InitializeXamlDiagnosticsEx(string endPointName, uint pid, string? wszDllXamlDiagnostics, string wszTAPDllName, Guid tapClsid, string? wszInitializationData)`;
  - rota externa: tentar `VisualDiagConnection1..10` até `S_OK`.
- [ ] **Passo 2: `RotaInterna.cs`.**
  - grava `init.txt`;
  - `LoadLibrary` da DLL e `GetProcAddress("VitralHookProc")`;
  - `SetWindowsHookEx(WH_CALLWNDPROC, proc, hmod, <thread de Shell_TrayWnd via GetWindowThreadProcessId>)`;
  - `SendMessage(Shell_TrayWnd, WM_NULL)`;
  - espera o evento Ready e então `UnhookWindowsHookEx`.
- [ ] **Passo 3: `Program.cs`.**
  - antes de tudo, se existir processo `TranslucentTB`, `windhawk` ou `ExplorerBlurMica`, imprime `Feche <nome> antes (só um consumidor de XAML Diagnostics por processo)` e sai com 3;
  - resolve o PID do alvo: `explorer` é o dono de `Shell_TrayWnd`; `iniciar` é `StartMenuExperienceHost` (pedir ao usuário que abra o Iniciar uma vez antes);
  - se o mutex `Local\VitralTap-<pid>` existir, sai com 2;
  - chama a rota, mede o tempo e grava o CSV.
- [ ] **Passo 4: build**
  Rodar: `dotnet build spike/injector -c Release`
  Esperado: `Build succeeded` com 0 erros.
- [ ] **Passo 5: preparação (com o Gustavo).** Confirmar com ele e fechar o TranslucentTB. Rodar `preparar-pasta.ps1`. Reiniciar o Explorer (`Stop-Process -Name explorer`; ele volta sozinho).
- [ ] **Passo 6: experimento A, rota externa, explorer, 10 rodadas.** Cada rodada: `Injector --rota externa --alvo explorer --modo transparente`, observar as 3 barras, reiniciar o Explorer.
  Esperado para "passou": código 0, as 3 barras transparentes, `FRAMES=3` no log.
- [ ] **Passo 7: experimento B, rota interna, explorer, 10 rodadas.** Mesmo protocolo com `--rota interna`.
- [ ] **Passo 8: casos do foco de revisão.**
  - (a) Com a DLL carregada, rodar o injector de novo: esperado código 2.
  - (b) `--manter` e depois `Stop-Process` no injector: esperado as barras voltarem ao normal em até 2 s e `RESTAURADO=` no log.
  - (c) Reiniciar o Explorer com a DLL carregada: esperado que ele suba com a barra normal, sem segunda queda em 5 min.
  - (d) Com o TranslucentTB aberto: esperado código 3.
- [ ] **Passo 9: experimento C, Iniciar.** Abrir o Iniciar uma vez e rodar `--alvo iniciar --modo log` pela rota externa (3 tentativas) e pela interna, adaptando o hook para a thread da janela do Iniciar (3 tentativas). Esperado para "passou": o log `tap-<pid>.log` do StartMenuExperienceHost contém elementos do Iniciar. Registrar o resultado mesmo se falhar.
- [ ] **Passo 10:** commit: `feat(spike): injector, rotas externa/interna e resultados`.

### Tarefa 5: UCPD

**Arquivos:**
- Criar: `spike/ucpd-probe/UcpdProbe.csproj` (`net10.0-windows`), `spike/ucpd-probe/Program.cs`

**Interfaces:**
- Produz: `UcpdProbe`. Para cada valor da lista abaixo, ele lê o valor atual e regrava **o mesmo valor e tipo**:
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDa`;
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\Search\SearchboxTaskbarMode`;
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarAl` (controle, não protegido).

  Imprime `chave;valor;OK|BLOQUEADO(<erro>)`. Se o valor não existir, imprime `AUSENTE` e não cria nada.

- [ ] **Passo 1:** implementar com `Microsoft.Win32.Registry` (`RegistryView.Registry64`).
- [ ] **Passo 2: verificar**
  Rodar: `dotnet run --project spike/ucpd-probe -c Release`
  Esperado: 3 linhas. `TaskbarAl` com `OK`. Os outros com `OK` ou `BLOQUEADO`: os dois resultados são válidos e vão para o relatório. Conferir depois, com `Get-ItemProperty`, que os valores não mudaram.
- [ ] **Passo 3:** commit: `feat(spike): sonda do UCPD`.

### Tarefa 6: relatório e decisão

**Arquivos:**
- Criar: `docs/superpowers/specs/2026-10-06-fase0-resultados.md`

- [ ] **Passo 1:** escrever o relatório:
  - tabela das rotas a partir de `rotas.csv` (taxa de sucesso e tempo médio por rota);
  - resultado dos 4 casos do foco de revisão;
  - resultado do Iniciar (externa e interna);
  - resultado do UCPD;
  - ferramenta usada (llvm-mingw ou plano B) e espaço consumido no C:;
  - problemas encontrados.
- [ ] **Passo 2: decisão, pela regra da spec §9.**
  - **Segue** se alguma rota deixou as 3 barras transparentes em pelo menos 9 de 10 rodadas, sem queda do Explorer fora do caso (c). A rota padrão da fase 1 é a de maior taxa de sucesso; no empate, a mais rápida.
  - **Para** se nenhuma rota passou.
  - Registrar também se o Iniciar entra pela rota escolhida, o que define o risco da fase 2.
- [ ] **Passo 3:** oferecer ao Gustavo reativar o TranslucentTB até a fase 1 ficar pronta.
- [ ] **Passo 4:** commit: `docs: resultados e decisão da fase 0`.
