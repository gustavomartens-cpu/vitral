# Vitral: design

Data: 2026-10-03 · Autor: Gustavo Martens (com Claude) · Estado: aguardando revisão

## 1. Objetivo

O Vitral é um app pessoal para Windows 11 que muda o visual do sistema e **substitui** as ferramentas que hoje fazem isso: TranslucentTB, Windhawk com os mods "Styler", barras de topo como YASB e Seelen UI, e gerenciadores de janelas como GlazeWM e komorebi. Ele não controla essas ferramentas: ele é a ferramenta.

O Gustavo monta "perfis visuais", testa sem risco, troca de perfil pela bandeja e leva os perfis para o notebook pessoal.

### Critérios de sucesso

1. A barra de tarefas fica transparente ou com vidro sem o TranslucentTB, com o mesmo comportamento por situação que ele usa hoje: transparente na área de trabalho, normal com o Iniciar ou o Task View abertos.
2. Os temas de barra escolhidos (FrostyGlass, LayerMicaUI, xdark, Matter, Command Center) aparecem aplicados pelo motor do próprio Vitral.
3. Qualquer mudança se desfaz com um clique. "Voltar ao Windows original" sempre funciona.
4. Um perfil exportado aqui é importado e aplicado no notebook sem ajustes manuais.
5. Falhas do componente nunca deixam o Windows num estado quebrado sem aviso.

### Fora do escopo (todas as fases)

- **Wallpaper Engine:** fica como está, pago e em uso. O Vitral não mexe no papel de parede, nem lê nem grava.
- Atalhos de teclado para trocar perfil e troca automática por regra (dia e noite, bateria, monitor).
- Rainmeter, cursores, sons, pacotes de ícones, `.msstyles` de terceiros.
- Substituições que trocam arquivos do sistema ou são invasivas: ExplorerPatcher, StartAllBack, DWMBlurGlass.
- Lista de tarefas e bandeja do sistema próprias. A barra de tarefas nativa continua existindo; o Vitral só a estiliza.
- Publicação do app. É uso pessoal: PC principal e notebook do Gustavo.

## 2. Ambiente

| | PC principal | Notebook |
|---|---|---|
| Windows | 11 Home SL 25H2, build 26200.9550 | Windows 11, pessoal (build e arquitetura a confirmar na 1ª execução) |
| Iniciar | Redesenhado (KB5067036) | a detectar |
| Monitores | 3, escala 100% | a detectar |
| Disco | C: ~35 GB livres; G: ~394 GB | — |
| Já instalado | TranslucentTB 2026.2 (Store), Rainmeter, Wallpaper Engine, Node 24 | — |

Restrições conhecidas:
- **UCPD** (UserChoice Protection Driver) ativo. Ele bloqueia escrita em `Explorer\Advanced\TaskbarDa`, `Explorer\Taskband` e chaves de `Search` quando feita por `reg.exe`, PowerShell, `cmd` etc. O bloqueio é por nome de processo; o executável do Vitral provavelmente passa, mas isso precisa ser **testado na fase 0**.
- **Só um consumidor de XAML Diagnostics por processo.** O Vitral não roda junto com TranslucentTB, Windhawk ou ExplorerBlurMica.
- Smart App Control desligado no PC principal. No notebook ainda não foi verificado.

## 3. Arquitetura

```
┌──────────────── App Vitral (C# · .NET 10 · WPF) ────────────────┐
│ Janela + Bandeja │ Motor de perfis │ Ajustes nativos │ Supervisor │
│ Barra do topo (F3) │ Tiling (F4)                                 │
└───────────────┬──────────────────────────────────────────────────┘
                │ carrega via XAML Diagnostics · canal: named pipe
┌───────────────▼──────── VitralTap.dll (C++ · C++/WinRT) ─────────┐
│ VisualTreeWatcher │ Motor de estilos │ Pincel de vidro │ Restauração│
└───────────────┬──────────────────────────────────────────────────┘
                ▼
 explorer.exe (barra: F1) · StartMenuExperienceHost / ShellExperienceHost (F2)
```

### 3.1 Componentes do app (C#)

| Componente | Responsabilidade | Depende de |
|---|---|---|
| `Vitral.Core.Profiles` | Modelo de perfil, leitura e gravação de `perfil.json`, validação de esquema, exportação e importação `.vitral` | — |
| `Vitral.Core.Snapshots` | Retrato e reversão de tudo que o app controla; retrato original; histórico dos últimos 10 | `IRegistry`, `ITapClient` |
| `Vitral.Core.Native` | Ajustes nativos via registro HKCU e broadcasts (`WM_SETTINGCHANGE "ImmersiveColorSet"`, `WM_THEMECHANGED`, `WM_DWMCOLORIZATIONCOLORCHANGED`); reinício controlado do Explorer quando necessário | `IRegistry` |
| `Vitral.Core.Themes` | Leitura do formato de temas dos styling-guides; cache local; classificação "só motor / parcial" | `HttpClient` |
| `Vitral.Core.Supervisor` | Carregar a TAP no `explorer.exe`, detectar reinício do Explorer (`TaskbarCreated`) e mudança de build, contar quedas, modo cauteloso, desligamento de segurança | `ITapClient` |
| `Vitral.App` | Janela WPF (Fluent, Mica), bandeja, avisos "Manter?" | Core |
| `Vitral.TopBar` (F3) | AppBar no topo com vidro e widgets | ManagedShell.AppBar |
| `Vitral.Tiling` (F4) | Zonas ao soltar e organização automática | — |

`IRegistry` e `ITapClient` são interfaces, para que o Core seja testável sem tocar no Windows.

### 3.2 VitralTap.dll (C++)

- DLL COM in-process carregada pelo próprio Windows via `InitializeXamlDiagnosticsEx` (`IObjectWithSite::SetSite` recebe `IXamlDiagnostics`). Assina a árvore visual com `IVisualTreeService3::AdviseVisualTreeChange` numa thread separada.
- **Motor de estilos:** regras "alvo → setters", no mesmo modelo dos Styler mods (seletor por tipo, nome e ancestrais; setters de propriedades; `styleConstants`; `themeResourceVariables`; variantes claro e escuro). Guarda o valor original de cada propriedade alterada e restaura sob comando.
- **Pincel de vidro:** `XamlCompositionBrushBase` com backdrop, desfoque gaussiano, cor de tinta e composição, usando efeitos D2D próprios (padrão do TranslucentTB).
- **Restauração automática:** observa o processo do app (`RegisterWaitForSingleObject`). Se o app morrer, restaura tudo.
- **Canal:** named pipe com DACL restrita ao usuário atual. O protocolo é versionado: `hello{versao}`, `aplicar{tema}`, `restaurar`, `estado` e `relatorio{regrasNaoEncontradas}`.
- **Proteções conhecidas a portar:** anexar só uma vez por PID; mitigação do crash em `AdviseVisualTreeChange`; contorno das "caixas brancas"; perda de transparência no 2º monitor após hot-plug.

### 3.3 Rotas de carga da TAP (decididas na fase 0)

1. **Externa:** o app chama `InitializeXamlDiagnosticsEx` por P/Invoke com o PID do Explorer, como faz o RainbowTaskbar. É a mais simples.
2. **Interna (rota atual do TranslucentTB):** `SetWindowsHookEx(WH_CALLWNDPROC)` na thread de `Shell_TrayWnd` carrega a DLL, que chama a API de dentro do Explorer. O TranslucentTB migrou para esta rota em 2024.3 por ter menos falhas.

A fase 0 mede as duas. A rota escolhida vira padrão, e a outra fica como plano B.

## 4. Perfis

### 4.1 Armazenamento

```
%LOCALAPPDATA%\Vitral\
  perfis\<nome>\perfil.json      ← perfil
  perfis\<nome>\temas\*.yaml     ← temas editados pelo usuário
  cache\temas\<repo>\<tema>\     ← temas baixados (não exportados)
  retratos\original.json         ← retrato da 1ª execução (nunca apagado)
  retratos\<data-hora>.json      ← últimos 10
  backup\primeira-execucao.reg   ← export das chaves que o app pode tocar
  logs\
  config.json                    ← perfil ativo, preferências
```

### 4.2 `perfil.json` (esquema v1)

```json
{
  "esquema": 1,
  "nome": "Vidro escuro",
  "buildTestada": "26200.9550",
  "nativo": {
    "modoSistema": "escuro", "modoApps": "escuro",
    "corDestaque": "#21375E", "corDestaqueAutomatica": false,
    "transparencia": true,
    "destaqueNaBarraEIniciar": true, "destaqueNosTitulos": true,
    "alinhamentoBarra": "centro",
    "posicaoBarra": null, "barraPequena": null
  },
  "barra": {
    "estilo": "tema",
    "tema": { "origem": "ramensoftware/windows-11-taskbar-styling-guide", "nome": "FrostyGlass", "versao": "<commit>" },
    "ajustes": { "opacidade": 0.35, "corVidro": "destaque" },
    "quando": {
      "areaDeTrabalho": "tema", "janelaMaximizada": "acrilico",
      "iniciarAberto": "normal", "taskViewAberto": "normal", "economiaDeBateria": "opaco"
    }
  }
}
```

- `null` significa "não mexer". A seção que falta também significa "não mexer".
- `estilo`: `normal` | `transparente` | `acrilico` | `opaco` | `tema`.
- `posicaoBarra` e `barraPequena` só são usados se a build tiver a KB5124008 (`TaskbarLocation` e `TaskbarSize`).
- As seções `iniciar`, `central` e `explorer` (F2), `barraTopo` (F3) e `tiling` (F4) entram no esquema v2+ com migração automática.

### 4.3 Retrato e reversão

- O retrato guarda cada valor de registro que o perfil pode tocar como `{chave, nome, tipo, dado | "ausente"}`, mais o estado da TAP (tema ativo ou nenhum).
- Reverter significa regravar os valores, apagar os que estavam "ausentes", repetir os broadcasts e mandar o tema anterior (ou `restaurar`) à TAP.
- **Testar:** retrato → aplicar → aviso "Manter? Volta em 20 s". Sem resposta, reverte.
- **Aplicar:** retrato → aplicar. A bandeja ganha "Desfazer última mudança".
- **Troca pela bandeja:** igual a Testar, com 12 s.
- **"Voltar ao Windows original":** desliga a TAP e reaplica `original.json`.

### 4.4 Exportar e importar

- `.vitral` é um zip com `manifest.json` (esquema, versão do app, build de origem, lista de perfis) e as pastas dos perfis com os temas editados. Temas baixados vão só como referência (`origem`, `nome`, `versao`).
- Na importação, o app valida o esquema, compara a build, baixa os temas referenciados, mostra a prévia e aplica via Testar.

## 5. Interface

Esboço aprovado: `docs/superpowers/mockups/janela-bandeja.html`, junto com os outros esboços da sessão de brainstorming.

- **Janela:**
  - barra lateral com os perfis (o ativo marcado), "+ Novo perfil", Importar/Exportar, Histórico de retratos, Configurações e o estado do componente, da build e do retrato original;
  - abas por área: Cores e tema · Barra de tarefas · Iniciar, Central e Explorer (F2) · Barra do topo (F3) · Janelas (F4). As abas futuras aparecem desabilitadas;
  - a aba Barra tem o estilo base (normal, transparente, acrílico, tema), a galeria de temas com foto e a etiqueta "só motor ✓" ou "parcial", os ajustes de opacidade e cor do vidro e a tabela "Quando…";
  - rodapé com Exportar perfil · Descartar · Testar (20 s) · Aplicar.
- **Bandeja:** perfis (✓ no ativo) · Desfazer última mudança · Voltar ao Windows original · Abrir Vitral · Sair.
- **Primeira execução:**
  1. detecta a build, o UCPD, a arquitetura e o Smart App Control;
  2. salva o retrato original e o `.reg` de backup;
  3. detecta o TranslucentTB (e o Windhawk, se houver) e **oferece** fechá-lo e tirá-lo da inicialização, com confirmação;
  4. cria um perfil "Meu Windows atual" a partir do estado presente.
- **Inicialização:** entrada em `HKCU\...\Run`, sem admin. Ao iniciar, reaplica o perfil ativo.

## 6. Erros

| Situação | Comportamento |
|---|---|
| A TAP não carrega | Barra no visual normal; ajustes nativos aplicados; tenta a rota B; aviso na bandeja; log |
| Regras do tema sem alvo nesta build | Aplica o resto; aviso "N de M regras não encontradas" na janela |
| O Explorer cai depois da carga | Duas quedas em 5 min: desliga a TAP, reverte para o visual normal e avisa. Só religa manualmente |
| A build mudou desde a última execução | Modo cauteloso: aplica, e qualquer falha reverte e avisa, sem novas tentativas |
| O UCPD ou outra causa bloqueia a escrita | Lista os ajustes não gravados; nunca informa sucesso parcial como total |
| Sem rede para baixar o tema | Usa o cache; sem cache, avisa e mantém o perfil anterior |
| `perfil.json` inválido ou de esquema futuro | Recusa com mensagem clara; não aplica nada |

Logs ficam em `%LOCALAPPDATA%\Vitral\logs\`, com rotação de 7 dias.

## 7. Testes

- **Automáticos (xUnit):** Core.Profiles (ida e volta do JSON, validação, migração de esquema), Core.Snapshots (retrato e reversão com registro falso, incluindo "ausente"), Core.Native (valores corretos por opção, ABGR da cor de destaque, `AccentPalette`), Core.Themes (parse de **todos** os temas atuais dos 4 styling-guides sem exceção) e protocolo do pipe (serialização e versão).
- **Componente nativo:** um programa de teste que carrega a TAP e grava a árvore visual da barra num log, usado para inspeção.
- **Roteiro manual a cada fase (PC principal):** aplicar e reverter; reiniciar o Explorer; ligar e desligar cada um dos 3 monitores; Iniciar e Task View; suspender e retomar; economia de bateria; "Voltar ao Windows original"; matar o app à força (a TAP precisa restaurar).
- **Notebook:** instalar o `Setup.exe`, importar `.vitral`, aplicar via Testar e cumprir o roteiro manual reduzido.

## 8. Ferramentas, build e instalação

- **Tudo em `G:\dev`, sem admin e sem Visual Studio:**
  - .NET 10 SDK em `G:\dev\dotnet` (`dotnet-install.ps1 -InstallDir`), com `NUGET_PACKAGES`, `NUGET_HTTP_CACHE_PATH` e `DOTNET_CLI_HOME` em `G:\dev`;
  - llvm-mingw 20260922 (ucrt x86_64) em `G:\dev\toolchains`;
  - `cppwinrt.exe` 2.0.250303.1, com a projeção gerada a partir dos `.winmd` do sistema ou do pacote NuGet do SDK.
- **Flags da TAP** (as mesmas do Windhawk): `-std=c++23 -O2 -shared -mguard=cf-nochecks -ffp-exception-behavior=maytrap -DUNICODE -D_UNICODE -D__USE_MINGW_ANSI_STDIO=0`. libc++ e libunwind são linkados **estaticamente** (obrigatório). Conferir com `llvm-objdump -p` que a DLL não importa `libc++.dll` nem `libunwind.dll`. Plano B: MSVC portátil.
- Mudanças em PATH ou variáveis de usuário só com o OK do Gustavo.
- `build.ps1` único compila a TAP, testa, publica o app self-contained (win-x64) e gera o `Setup.exe` (Velopack, instalação por usuário, sem UAC).
- A TAP é copiada para uma pasta do app com ACL de leitura para `ALL APPLICATION PACKAGES`, necessária para os hosts empacotados da F2.
- Atualização manual na v1: rodar o `Setup.exe` novo.

## 9. Fases

| Fase | Entrega | Critério de pronto |
|---|---|---|
| **0: prova de viabilidade** (descartável) | Ferramentas instaladas; TAP mínima que deixa a barra transparente no 26200.9550; medição das rotas externa e interna; teste de carga no `StartMenuExperienceHost`; teste de escrita do exe próprio em chave protegida pelo UCPD | Barra transparente por um TAP nosso; rota escolhida; resultado do Iniciar e do UCPD documentados. **Se a barra não funcionar por nenhuma rota, o projeto para e voltamos ao desenho.** |
| **1: nativo + barra** | Core completo, janela, bandeja, ajustes nativos, TAP com motor de estilos e pincel de vidro para a barra, estados "Quando…", supervisor, exportar e importar, instalador | Critérios 1, 3, 4 e 5 cumpridos; FrostyGlass, LayerMicaUI, xdark, Matter e Command Center aplicados |
| **2: Iniciar, central, Explorer** | TAP nos hosts do Iniciar e da central (ACL e pipe para AppContainer); Explorer por último; DSL de variáveis para LiquidGlass2 e OS26 | Temas de Iniciar e central dos favoritos aplicados; Explorer opcional dentro da fase |
| **3: barra do topo** | AppBar WPF no topo com vidro (spike de 1 dia para o vidro com janela inativa), relógio, mídia, volume, bateria, rede, ocultar em tela cheia | Usável no dia a dia nos 3 monitores |
| **4: tiling** | B0: zonas ao soltar e botões na barra do topo. B1: organização automática opcional por monitor ou app, sem workspaces próprios | Uso diário com os apps do Gustavo |

As fases 3 e 4 não dependem da TAP e podem ser antecipadas se a fase 2 travar.

## 10. Riscos

| Risco | Mitigação |
|---|---|
| Atualizações do Windows mudam nomes internos do XAML | Relatório de regras não encontradas; modo cauteloso após mudança de build; temas puxados da versão mais recente dos guias |
| Crash do Explorer causado pela TAP | Supervisor com desligamento de segurança; TAP sem reanexar; restauração quando o app morre |
| Rota externa instável (motivo da mudança no TranslucentTB) | Fase 0 mede; rota interna como plano B |
| Hosts AppContainer (Iniciar e central) recusam a carga externa | Testado na fase 0; injetor nativo na F2 se necessário |
| Temas que dependem de mods "companheiros" (altura, ícones, dock; ~40% dos temas de barra) | Etiqueta "parcial" na galeria; não implementar hooks por símbolo (PDB) nas fases 1 e 2 |
| Antivírus ou Smart App Control no notebook | Verificação na 1ª execução com orientação |
| Licenças | Uso pessoal: sem obrigação prática. TAP isolada (GPL-3 se portar código do TranslucentTB ou do Windhawk). Temas sem licença baixados em tempo de execução, nunca embutidos. **Não copiar** código do komorebi nem do FancyWM. Arquivo `NOTICE` com a origem de cada trecho portado |

## 11. Referências

- TranslucentTB (GPL-3.0): `ExplorerTAP` (`api.cpp`, `tapsite.cpp`, `visualtreewatcher.cpp`, `XamlBlurBrush.cpp`, `taskbarappearanceservice.cpp`).
- windhawk-mods: `windows-11-taskbar-styler` v1.10, `start-menu-styler` v1.7, `notification-center-styler` v1.7 e `file-explorer-styler` v1.7 (motor de seletores e setters, mitigações).
- `ramensoftware/windows-11-*-styling-guide`: formato dos temas (sem LICENSE; baixar em tempo de execução).
- RainbowTaskbar (MIT): P/Invoke de `InitializeXamlDiagnosticsEx` a partir de C#.
- ManagedShell (Apache-2.0): AppBar para a fase 3. YASB (MIT): widgets e vidro.
- GlazeWM v2.1.1 (C#, GPL-3.0), workspacer e Whim (MIT): referência de tiling. `applications.json` do komorebi (MIT): regras por app.
- Pesquisa completa desta sessão (3 rodadas, 22 agentes com verificação cética) resumida em `docs/pesquisa/2026-10-03-notas.md`.

## 12. Decisões registradas

1. **Substituir** as ferramentas em vez de controlá-las (pedido do Gustavo, 03/10). O Wallpaper Engine fica.
2. C# .NET 10 + WPF para o app; C++ com llvm-mingw para a TAP.
3. Ordem das fases: barra de baixo primeiro.
4. Uso diário pela janela e pela bandeja. Sem atalhos e sem troca automática.
5. Editor por área: os conjuntos de temas servem só como ponto de partida.
6. Projeto em `G:\dev\vitral`, git local, sem remoto por enquanto.
