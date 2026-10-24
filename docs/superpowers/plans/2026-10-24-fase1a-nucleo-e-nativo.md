# Fase 1a: núcleo e ajustes nativos (plano de implementação)

> **Para agentes:** sub-skill obrigatória: use superpowers:subagent-driven-development (recomendado) ou superpowers:executing-plans para executar este plano tarefa por tarefa. Os passos usam checkbox (`- [ ]`) para acompanhamento.

**Objetivo:** construir o núcleo do Vitral e um comando de linha `vitral` que aplica, testa e reverte os **ajustes nativos** de um perfil (modo claro e escuro, cor de destaque, transparência, destaque na barra e nos títulos, alinhamento da barra), além de exportar e importar perfis `.vitral`. Tudo com testes automáticos que não tocam no Windows real.

**Arquitetura:** biblioteca `Vitral.Core` (sem interface) com as interfaces `IRegistro` e `INotificador`, que isolam o Windows; implementações reais em `Vitral.Core/Windows`. O console `Vitral.Cli` serve para usar o núcleo de verdade antes da janela (fase 1d). Os testes xUnit usam um registro em memória.

**Stack:** .NET 10 (C#), xUnit, System.Text.Json, Microsoft.Win32.Registry (visão de 64 bits), P/Invoke para `SendMessageTimeoutW`.

**Spec:** `docs/superpowers/specs/2026-10-03-vitral-design.md` (§3.1 componentes, §4 perfis, §6 erros, §7 testes). Contexto da fase 0: `docs/superpowers/specs/2026-10-06-fase0-resultados.md`.

**Série da fase 1:** 1a núcleo e nativo (este) → 1b barra de produção (componente, estados, supervisor) → 1c motor de estilos e vidro → 1d janela, bandeja e instalador.

## Restrições globais

- Ferramentas e caches em `G:\dev`. Ative com `. tools/env.ps1` (cópia de `spike/tools/env.ps1` na raiz, criada na Tarefa 1). Sem admin; sem mudar o PATH do sistema.
- Escrita **só em HKCU**, sempre em `RegistryView.Registry64`. Nada em HKLM.
- Nomes de tipos e membros em português, como na spec (`Perfil`, `Retrato`, `AjustesNativos`...).
- `null` em qualquer campo do perfil = "não mexer". Seção ausente = "não mexer".
- Esquema do perfil: `"esquema": 1`. Perfil com esquema maior que o suportado é **recusado** sem aplicar nada (spec §6).
- Cores no perfil em `#RRGGBB`. No registro: `AccentColor`, `AccentColorMenu` e `StartColorMenu` são DWORD **ABGR** (`0xFFBBGGRR`); `ColorizationColor` é DWORD **ARGB** (`0xC4RRGGBB`, alfa fixo `0xC4`, o mesmo que esta máquina usa hoje).
- Dados do app em `%LOCALAPPDATA%\Vitral\` (spec §4.1). O `retratos\original.json` nunca é apagado.
- Nunca relatar sucesso parcial como total (spec §6). Escrita bloqueada (o UCPD, por exemplo) vira item em `Resultado.NaoAplicados`.
- Testes nunca tocam o registro real nem fazem broadcast: usam `RegistroEmMemoria` e `NotificadorFalso`.

**Fica para planos seguintes, de propósito:** `posicaoBarra` e `barraPequena` (dependem da KB5124008 e de detecção da build; entram na 1d junto com a interface), logs com rotação de 7 dias (1d), seção `barra` do perfil (1b), primeira execução e detecção do TranslucentTB (1d).

## Foco de revisão

1. **Valor que não existia antes de aplicar:** reverter precisa **apagar** o valor, não gravar zero. Teste na Tarefa 3.
2. **Escrita bloqueada no meio da aplicação** (UCPD, `UnauthorizedAccessException`): o resto do perfil aplica, o item bloqueado é listado e o comando sai com código 2. Teste na Tarefa 4.
3. **Falha inesperada no meio da aplicação** (exceção que não é bloqueio): o aplicador desfaz o que já gravou e devolve o estado do retrato. Teste na Tarefa 4.
4. **Perfil de esquema futuro ou com cor inválida** (`"#12345"`, `"azul"`): recusado com mensagem clara, nada aplicado. Teste na Tarefa 2.
5. **Importar no notebook** (outro usuário e outro caminho): o pacote não carrega caminhos absolutos e o import grava na pasta do usuário atual. Teste na Tarefa 5.

---

### Tarefa 1: solução, registro abstrato e cores

**Arquivos:**
- Criar: `Vitral.sln`, `Directory.Build.props` (`net10.0-windows`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`), `tools/env.ps1`
- Criar: `src/Vitral.Core/Vitral.Core.csproj`, `src/Vitral.Core/Registro/IRegistro.cs`, `src/Vitral.Core/Registro/ValorRegistro.cs`, `src/Vitral.Core/Registro/RegistroEmMemoria.cs`, `src/Vitral.Core/Cores/Cor.cs`
- Criar: `tests/Vitral.Core.Tests/Vitral.Core.Tests.csproj` (xUnit), `tests/Vitral.Core.Tests/CorTests.cs`, `tests/Vitral.Core.Tests/RegistroEmMemoriaTests.cs`

**Interfaces:**
- Produz:
  - `enum TipoValor { Dword, Binario, Texto }`
  - `sealed record ValorRegistro(string Chave, string Nome, TipoValor Tipo, object? Dado)`: `Dado == null` significa **ausente**; `Chave` é relativa a HKCU, por exemplo `Software\Microsoft\Windows\DWM`.
  - `interface IRegistro { ValorRegistro Ler(string chave, string nome, TipoValor tipoEsperado); void Gravar(ValorRegistro v); void Apagar(string chave, string nome); }`. `Gravar` lança `EscritaBloqueadaException(ValorRegistro)` quando o Windows nega acesso.
  - `sealed class RegistroEmMemoria : IRegistro`, com `HashSet<(string,string)> Bloqueados` para simular o UCPD.
  - `readonly record struct Cor(byte R, byte G, byte B)` com `static Cor DeHex(string)` (aceita `#RRGGBB`; senão lança `FormatException`), `string ParaHex()`, `uint ParaAbgr()` (= `0xFF000000 | B<<16 | G<<8 | R`), `uint ParaArgb(byte alfa)`, `static Cor DeAbgr(uint)`, `Cor Misturar(Cor outra, double t)`.

- [ ] **Passo 1:** criar `tools/env.ps1` (cópia de `spike/tools/env.ps1`), a solução e os dois projetos. Verificar: `. tools/env.ps1; dotnet build` → `0 Erro(s)`.
- [ ] **Passo 2: testes que falham** (`CorTests`):
  - `DeHex("#21375E")` → `R=0x21, G=0x37, B=0x5E`;
  - `ParaAbgr()` de `#21375E` → `0xFF5E3721`;
  - `DeAbgr(0xFF110A06).ParaHex()` → `"#060A11"` (o valor real de `DWM\AccentColor` nesta máquina);
  - `ParaArgb(0xC4)` de `#986A31` → `0xC4986A31`;
  - `DeHex` lança `FormatException` para `"#12345"`, `"azul"` e `"21375E"`.

  `RegistroEmMemoriaTests`: ler um valor inexistente devolve `Dado == null`; gravar e ler devolve o mesmo valor; `Gravar` num par em `Bloqueados` lança `EscritaBloqueadaException`; `Apagar` faz a próxima leitura devolver ausente.
- [ ] **Passo 3:** rodar `dotnet test` → FAIL (os tipos não existem).
- [ ] **Passo 4:** implementar `Cor`, `ValorRegistro`, `IRegistro`, `RegistroEmMemoria` e `EscritaBloqueadaException`.
- [ ] **Passo 5:** `dotnet test` → todos PASS.
- [ ] **Passo 6:** commit `feat(core): solução, registro abstrato e cores`.

### Tarefa 2: modelo de perfil e JSON

**Arquivos:**
- Criar: `src/Vitral.Core/Perfis/Perfil.cs`, `src/Vitral.Core/Perfis/PerfilJson.cs`, `tests/Vitral.Core.Tests/PerfilJsonTests.cs`

**Interfaces:**
- Consome: `Cor` (Tarefa 1).
- Produz:
  - `sealed record Perfil(int Esquema, string Nome, string? BuildTestada, AjustesNativosPerfil? Nativo)`
  - `sealed record AjustesNativosPerfil(Modo? ModoSistema, Modo? ModoApps, string? CorDestaque, bool? CorDestaqueAutomatica, bool? Transparencia, bool? DestaqueNaBarraEIniciar, bool? DestaqueNosTitulos, Alinhamento? AlinhamentoBarra)`
  - `enum Modo { Claro, Escuro }`, `enum Alinhamento { Esquerda, Centro }`
  - `static class PerfilJson { const int EsquemaSuportado = 1; Perfil Ler(string json); string Escrever(Perfil p); }`. `Ler` lança `PerfilInvalidoException(string motivo)`.
  - JSON em camelCase, com enums como texto minúsculo (`"escuro"`, `"centro"`), no formato do exemplo da spec §4.2. A seção `barra` e as outras seções futuras são **ignoradas** sem erro: são das fases 1b em diante.

- [ ] **Passo 1: testes que falham:**
  - ler o exemplo da spec §4.2 (copiado como texto no teste) devolve `Nome="Vidro escuro"`, `Nativo.CorDestaque="#21375E"`, `Nativo.ModoSistema=Escuro`, `Nativo.AlinhamentoBarra=Centro`;
  - `Escrever` e depois `Ler` devolve um perfil igual (ida e volta);
  - campo `null` continua `null`;
  - `"esquema": 2` → `PerfilInvalidoException` com mensagem contendo `"esquema 2"`;
  - `"corDestaque": "azul"` → `PerfilInvalidoException` com mensagem contendo `"corDestaque"`;
  - JSON quebrado → `PerfilInvalidoException`.
- [ ] **Passo 2:** `dotnet test --filter PerfilJsonTests` → FAIL.
- [ ] **Passo 3:** implementar com `System.Text.Json` (`JsonNamingPolicy.CamelCase` e `JsonStringEnumConverter` com camelCase). A validação da cor usa `Cor.DeHex`.
- [ ] **Passo 4:** `dotnet test` → PASS.
- [ ] **Passo 5:** commit `feat(core): modelo de perfil e leitura/gravação JSON`.

### Tarefa 3: ajustes nativos (valores) e retrato

**Arquivos:**
- Criar: `src/Vitral.Core/Nativo/AjustesNativos.cs`, `src/Vitral.Core/Retratos/Retrato.cs`, `src/Vitral.Core/Retratos/ServicoRetratos.cs`
- Teste: `tests/Vitral.Core.Tests/AjustesNativosTests.cs`, `tests/Vitral.Core.Tests/ServicoRetratosTests.cs`

**Interfaces:**
- Consome: `IRegistro`, `ValorRegistro`, `Cor`, `AjustesNativosPerfil`.
- Produz:
  - `static class AjustesNativos { IReadOnlyList<(string Chave, string Nome, TipoValor Tipo)> Controlados { get; } IReadOnlyList<ValorRegistro> Calcular(AjustesNativosPerfil a); byte[] Paleta(Cor baseCor); }`
  - `sealed record Retrato(DateTimeOffset Quando, string? PerfilAplicado, IReadOnlyList<ValorRegistro> Valores)`
  - `sealed class ServicoRetratos(IRegistro registro, string pasta) { Retrato Tirar(string? perfil); void Salvar(Retrato r); Retrato? Ultimo(); Retrato GarantirOriginal(); IReadOnlyList<Retrato> Historico(); }`. Mantém só os 10 mais recentes, e `original.json` nunca entra na conta nem é apagado.

**Tabela de `Calcular`** (só gera valores para campos não-nulos; chaves relativas a HKCU):

| Campo | Chave | Valor(es) |
|---|---|---|
| `ModoSistema` | `Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` | `SystemUsesLightTheme` = 1 (claro) / 0 (escuro) |
| `ModoApps` | idem | `AppsUseLightTheme` = 1 / 0 |
| `Transparencia` | idem | `EnableTransparency` = 1 / 0 |
| `DestaqueNaBarraEIniciar` | idem | `ColorPrevalence` = 1 / 0 (**forçado 0** se `ModoSistema == Claro`) |
| `DestaqueNosTitulos` | `Software\Microsoft\Windows\DWM` | `ColorPrevalence` = 1 / 0 |
| `CorDestaqueAutomatica` | `Control Panel\Desktop` | `AutoColorization` = 1 / 0 |
| `CorDestaque` (c) | `Software\Microsoft\Windows\DWM` | `AccentColor` = `c.ParaAbgr()`, `ColorizationColor` = `c.ParaArgb(0xC4)`; também grava `AutoColorization` = 0 |
| `CorDestaque` (c) | `Software\Microsoft\Windows\CurrentVersion\Explorer\Accent` | `AccentPalette` = `Paleta(c)`, `AccentColorMenu` = ABGR do tom 4 da paleta, `StartColorMenu` = ABGR do tom 5 |
| `AlinhamentoBarra` | `Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced` | `TaskbarAl` = 0 (esquerda) / 1 (centro) |

`Paleta(c)`: 8 entradas de 4 bytes `R,G,B,0xFF`. As 3 primeiras são `c` misturada com branco (t = 0,6 / 0,4 / 0,2); a 4ª é `c`; da 5ª à 7ª, `c` misturada com preto (t = 0,2 / 0,4 / 0,6); a 8ª é fixa `88 17 98 00` (o mesmo valor que o Windows grava nesta máquina).

- [ ] **Passo 1: testes que falham** (`AjustesNativosTests`):
  - com tudo `null`, `Calcular` devolve lista vazia;
  - `ModoSistema=Claro` + `DestaqueNaBarraEIniciar=true` → `Personalize\ColorPrevalence = 0`;
  - `CorDestaque="#21375E"` → `DWM\AccentColor = 0xFF5E3721` e `AutoColorization = 0`;
  - `Paleta` tem 32 bytes, o tom 4 é `21 37 5E FF` e os bytes 28 a 31 são `88 17 98 00`;
  - todo valor devolvido por `Calcular` está em `Controlados`.

  `ServicoRetratosTests`, com `RegistroEmMemoria` e uma pasta temporária:
  - `Tirar` registra como **ausente** um valor controlado que não existe;
  - `GarantirOriginal` cria `original.json` só na primeira chamada (a segunda devolve o mesmo `Quando`);
  - depois de 12 `Salvar`, o `Historico()` tem 10 itens e o `original.json` ainda existe.
- [ ] **Passo 2:** `dotnet test` → FAIL.
- [ ] **Passo 3:** implementar. O retrato é salvo como JSON (`retratos\aaaaMMdd-HHmmssfff.json`), com binário em base64.
- [ ] **Passo 4:** `dotnet test` → PASS.
- [ ] **Passo 5:** commit `feat(core): cálculo dos ajustes nativos e retratos`.

### Tarefa 4: aplicador com reversão

**Arquivos:**
- Criar: `src/Vitral.Core/Nativo/INotificador.cs`, `src/Vitral.Core/Aplicacao/Aplicador.cs`, `src/Vitral.Core/Aplicacao/Resultado.cs`
- Teste: `tests/Vitral.Core.Tests/AplicadorTests.cs` (com `NotificadorFalso` no próprio arquivo de teste)

**Interfaces:**
- Consome: `IRegistro`, `ServicoRetratos`, `AjustesNativos`, `Perfil`.
- Produz:
  - `interface INotificador { void AvisarMudancaDeTema(); void AvisarMudancaDaBarra(); }`
  - `sealed record Resultado(bool Ok, IReadOnlyList<ValorRegistro> Aplicados, IReadOnlyList<(ValorRegistro Valor, string Motivo)> NaoAplicados, Retrato RetratoAnterior)`
  - `sealed class Aplicador(IRegistro registro, ServicoRetratos retratos, INotificador notificador) { Resultado Aplicar(Perfil p); void Reverter(Retrato r); }`

**Regras:**
- `Aplicar`: `GarantirOriginal()` → `Tirar` + `Salvar` (o retrato anterior) → grava cada valor de `Calcular`.
  - `EscritaBloqueadaException` vira item em `NaoAplicados` e a aplicação continua.
  - Qualquer outra exceção faz `Reverter(retratoAnterior)` e é relançada.
  - No fim, `AvisarMudancaDeTema()` uma vez e, se `TaskbarAl` mudou, `AvisarMudancaDaBarra()`.
  - `Ok = NaoAplicados.Count == 0`.
- `Reverter`: para cada valor do retrato, `Dado == null` → `Apagar`; senão `Gravar`. Depois, os mesmos avisos.

- [ ] **Passo 1: testes que falham:**
  - aplicar um perfil com `CorDestaque` grava os valores e chama `AvisarMudancaDeTema` uma vez;
  - **Foco 1:** `TaskbarAl` ausente antes, aplicar `Esquerda` e depois `Reverter(resultado.RetratoAnterior)` → `TaskbarAl` volta a **ausente** (não 0);
  - **Foco 2:** com `Personalize\ColorPrevalence` em `Bloqueados`, `Resultado.Ok == false`, `NaoAplicados` com 1 item e os outros valores gravados;
  - **Foco 3:** com um `IRegistro` de teste que lança `IOException` no 3º `Gravar`, `Aplicar` relança e o registro fica igual ao retrato anterior;
  - `AvisarMudancaDaBarra` é chamado só quando `AlinhamentoBarra` não é nulo.
- [ ] **Passo 2:** `dotnet test --filter AplicadorTests` → FAIL.
- [ ] **Passo 3:** implementar.
- [ ] **Passo 4:** `dotnet test` → PASS (todos os testes da solução).
- [ ] **Passo 5:** commit `feat(core): aplicador com retrato, bloqueios e reversão`.

### Tarefa 5: pacote `.vitral` (exportar e importar)

**Arquivos:**
- Criar: `src/Vitral.Core/Perfis/RepositorioPerfis.cs`, `src/Vitral.Core/Perfis/Pacote.cs`
- Teste: `tests/Vitral.Core.Tests/PacoteTests.cs`

**Interfaces:**
- Consome: `Perfil`, `PerfilJson`.
- Produz:
  - `sealed class RepositorioPerfis(string pastaPerfis) { IReadOnlyList<string> Nomes(); Perfil Carregar(string nome); void Salvar(Perfil p); }`. A pasta de cada perfil é `<pastaPerfis>\<nome sem caracteres inválidos de arquivo>\perfil.json`.
  - `static class Pacote { void Exportar(RepositorioPerfis repo, IEnumerable<string> nomes, string destinoZip, string versaoApp, string build); IReadOnlyList<Perfil> Importar(string origemZip, RepositorioPerfis destino); }`
  - Zip com `manifest.json` (`{ "esquema":1, "versaoApp", "buildOrigem", "perfis":[nomes] }`) e `perfis/<nome>/perfil.json`. Sem nenhum caminho absoluto.

- [ ] **Passo 1: testes que falham:**
  - **Foco 5:** exportar 2 perfis de um repositório em `tmp\A`, importar num repositório em `tmp\B` → os 2 perfis carregam iguais em `B`, e nenhuma entrada do zip contém `:` ou começa com `\`;
  - zip sem `manifest.json` → `PerfilInvalidoException`;
  - manifest com `"esquema": 2` → recusado e nada gravado em `B`;
  - nome de perfil `"Vidro/escuro"` gera a pasta `Vidro_escuro`.
- [ ] **Passo 2:** `dotnet test --filter PacoteTests` → FAIL.
- [ ] **Passo 3:** implementar com `System.IO.Compression.ZipArchive`.
- [ ] **Passo 4:** `dotnet test` → PASS.
- [ ] **Passo 5:** commit `feat(core): exportar e importar perfis .vitral`.

### Tarefa 6: Windows real (registro, avisos) e comando `vitral`

**Arquivos:**
- Criar: `src/Vitral.Core/Windows/RegistroWindows.cs`, `src/Vitral.Core/Windows/NotificadorWindows.cs`, `src/Vitral.Core/Windows/Pastas.cs`
- Criar: `src/Vitral.Cli/Vitral.Cli.csproj` (exe, `AssemblyName=vitral`), `src/Vitral.Cli/Program.cs`
- Teste: `tests/Vitral.Core.Tests/CliTests.cs` (só a interpretação dos argumentos: sem registro real)

**Interfaces:**
- Consome: tudo das Tarefas 1 a 5.
- Produz:
  - `RegistroWindows : IRegistro` (HKCU, `Registry64`; `UnauthorizedAccessException` → `EscritaBloqueadaException`)
  - `NotificadorWindows : INotificador`:
    - `AvisarMudancaDeTema`: `SendMessageTimeoutW(HWND_BROADCAST, WM_SETTINGCHANGE, 0, "ImmersiveColorSet", SMTO_ABORTIFHUNG, 5000)`, depois `WM_THEMECHANGED` e `WM_DWMCOLORIZATIONCOLORCHANGED` em broadcast;
    - `AvisarMudancaDaBarra`: `WM_SETTINGCHANGE` com `"TraySettings"`.
  - `Pastas.Raiz = %LOCALAPPDATA%\Vitral`, com `Perfis`, `Retratos` e `Logs`.
  - Comandos (códigos de saída: 0 ok, 1 erro de uso ou perfil inválido, 2 aplicado com itens não gravados):
    - `vitral retrato` imprime os valores controlados atuais;
    - `vitral salvar-atual <nome>` cria um perfil com o estado atual;
    - `vitral aplicar <nome>`;
    - `vitral testar <nome> [--segundos 20]` aplica, pergunta `Manter? (s/n)` e reverte se a resposta não for `s` dentro do prazo;
    - `vitral desfazer` reverte o último retrato;
    - `vitral original` reverte para o `original.json`;
    - `vitral exportar <arquivo.vitral> <nome...>`;
    - `vitral importar <arquivo.vitral>`.

- [ ] **Passo 1: testes que falham** (`CliTests`): `Comando.Interpretar(["testar","Vidro escuro","--segundos","5"])` devolve `Testar("Vidro escuro", 5)`; sem argumentos devolve `Uso`; um comando desconhecido devolve `Uso`.
- [ ] **Passo 2:** `dotnet test --filter CliTests` → FAIL.
- [ ] **Passo 3:** implementar `Comando.Interpretar`, `Program.cs`, `RegistroWindows`, `NotificadorWindows` e `Pastas`.
- [ ] **Passo 4:** `dotnet test` → PASS. `dotnet build -c Release` → `0 Erro(s)`.
- [ ] **Passo 5: verificação real com o Gustavo** (pede autorização antes; mexe nas cores do Windows dele):
  - `vitral salvar-atual "Meu Windows atual"`, depois `vitral retrato`. Esperado: os valores batem com `Get-ItemProperty` das mesmas chaves.
  - Criar um perfil de teste com `ModoApps=Claro` e `CorDestaque="#E81123"` e rodar `vitral testar teste --segundos 20` sem responder. Esperado: as janelas ficam claras e o destaque fica vermelho; depois de 20 s, tudo volta. Se a cor do destaque não atualizar na hora, registrar no relatório (a pesquisa indicou que pode exigir reiniciar o Explorer); **não** reiniciar o Explorer automaticamente nesta fase.
  - `vitral original`. Esperado: os valores voltam aos do `original.json`.
- [ ] **Passo 6:** commit `feat(cli): comando vitral com registro e avisos reais`.
