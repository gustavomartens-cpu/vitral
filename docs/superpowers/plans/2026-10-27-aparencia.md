# Aparência da doca, do notch e das ilhas — plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** o perfil escolhe a aparência (sete temas, personalizar, diferenciar por parte) da doca, do notch, das ilhas e
dos painéis, com uma aba "Aparência" e prévia.

**Architecture:** uma regra pura no Core transforma `AparenciaPerfil` + o visual do Windows em `ValoresAparencia` (cores
prontas por parte). Um `EstadoAparencia` guarda a aparência aplicada (config, retratos, aplicador), e um
`ServicoAparencia` no App publica os valores em recursos dinâmicos que a doca, o notch, as ilhas e os painéis leem.

**Tech Stack:** .NET 10, WPF, xUnit. Testes: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet test --nologo -v q'`.

**Spec:** `docs/superpowers/specs/2026-10-27-aparencia-design.md`

## Global Constraints

- Temas, nesta ordem: `Automatico, VidroEscuro, PretoSolido, Claro, VidroColorido, Grafite, Cristal`; valores da tabela
  do spec §4.1 (fundo, opacidade, destaque, notch); todos com cantos `Redondos`.
- Sem aparência guardada e sem perfil com a seção: Vidro escuro.
- `Transparencia` 0 (opaco) a 100 (invisível), recortada; opacidade = 100 − transparência.
- Texto escuro `#1B1D22` quando luminância relativa do fundo > 0,5 **e** opacidade ≥ 50%; senão `#ECEEF2`.
  `TextoSecundario` = texto a 62%. Borda: branco 10% (texto claro) / preto 8% (texto escuro).
- Cantos: Retos 0 px, Suaves 4 px, Redondos 8 px; no DWM: não arredondar / pequeno / padrão.
- Notch nunca com desfoque; transparência do Windows desligada → todo Vidro vira Sólido a 100%.
- Chaves de recurso: `Aparencia.<Doca|Notch|Ilhas>.<Fundo|Texto|TextoSecundario|Borda|Destaque|Raio>`.
- A janela principal do Vitral não muda de aparência.

## Review Focus

1. **Tema Claro com glifos e ícones pensados para fundo escuro:** tudo legível. Conferência na Task 5/6 com o Gustavo,
   item por item (ilhas, painéis, doca, menu, card das janelas, notch, fichas).
2. **Windows muda de modo com o Automático e o Vitral aberto:** tudo redesenha sem reaplicar. Teste
   `Automatico_segue_o_modo_e_o_destaque_do_Windows` (Task 1) + conferência na Task 7.
3. **Perfil importado de versão antiga (sem `aparencia`) ou com cor inválida:** não quebra, não mexe. Testes
   `Perfil_sem_aparencia_nao_mexe` e `Cor_invalida_e_ignorada` (Tasks 1 e 2).
4. **Testar (20 s) / Desfazer / Voltar ao original com a aparência:** volta exatamente. Teste
   `Retrato_guarda_e_devolve_a_aparencia` (Task 2).
5. **Transparência do Windows desligada:** vidro vira sólido em todas as partes e na prévia. Teste
   `Transparencia_do_Windows_desligada_vira_solido` (Task 1).

---

### Task 1: Modelo e regra (Core)

**Files:**
- Create: `src/Vitral.Core/Perfis/AparenciaPerfil.cs`, `src/Vitral.Core/Aparencia/ResolverAparencia.cs`
- Modify: `src/Vitral.Core/Perfis/Perfil.cs` (`AparenciaPerfil? Aparencia = null` no fim)
- Test: `tests/Vitral.Core.Tests/AparenciaTests.cs`

**Interfaces:**
- Produces: os tipos do spec §3 (`TemaAparencia`, `MaterialParte`, `CantosParte`, `AjustesAparencia`,
  `AparenciaPerfil`) e §4:
  - `readonly record struct Cor(byte A, byte R, byte G, byte B)` com `static Cor? DeHex(string? hex, byte alfa = 255)`;
  - `record VisualWindows(bool ModoEscuro, string CorDestaque, bool Transparencia)`;
  - `record ValoresParte(MaterialParte Material, Cor Fundo, Cor Texto, Cor TextoSecundario, Cor Borda, Cor Destaque, CantosParte Cantos)` com `double RaioPx`;
  - `record ValoresAparencia(ValoresParte Doca, ValoresParte Notch, ValoresParte Ilhas)`;
  - `static ValoresAparencia ResolverAparencia.Resolver(AparenciaPerfil? aparencia, VisualWindows windows)` (nulo =
    Vidro escuro).

- [ ] **Step 1: testes**

```csharp
[Theory] /* os sete temas: fundo, alfa (opacidade×255), destaque, material, notch */ Tema_da_os_valores_da_tabela(...)
[Fact] Automatico_segue_o_modo_e_o_destaque_do_Windows() // escuro → #141826 55% + destaque do Windows; claro → #FAFAFC 72%, notch #F4F4F6
[Fact] Camadas_tema_geral_parte() // Geral CorFundo vale nas três; Notch.CorFundo só no notch, por cima do geral
[Fact] Texto_legivel() // Claro → texto #1B1D22; Cristal (branco 8%) → #ECEEF2; fundo personalizado #F4F4F6 num tema escuro → texto escuro
[Fact] Transparencia_do_Windows_desligada_vira_solido() // VidroEscuro: Material Solido, alfa 255, nas três
[Fact] Notch_nunca_com_desfoque() // Vidro colorido: notch Material Vidro com alfa 217 (85%) e marcado sem desfoque (o App não aplica acrílico no notch)
[Fact] Transparencia_e_recortada() // -10 → opaco; 140 → alfa 0
[Fact] Cor_invalida_e_ignorada() // CorFundo "azul" → fundo do tema
[Fact] Perfil_sem_aparencia_nao_mexe() // Resolver(null) == Resolver(VidroEscuro); PerfilJson ida e volta com e sem a seção
[Fact] Cantos_em_px() // Retos 0, Suaves 4, Redondos 8
```

- [ ] **Step 2:** rodar → FAIL (tipos novos).
- [ ] **Step 3:** implementar. Luminância relativa sRGB (WCAG) do fundo sem alfa.
- [ ] **Step 4:** rodar → PASS, suíte inteira verde.
- [ ] **Step 5:** commit `feat(core): aparência no perfil e a regra dos temas`.

### Task 2: Estado, retratos, aplicador e pacote (Core)

**Files:**
- Create: `src/Vitral.Core/Aparencia/EstadoAparencia.cs`
- Modify: `src/Vitral.Core/Configuracao/ConfigApp.cs` (`AparenciaPerfil? Aparencia = null`),
  `src/Vitral.Core/Retratos/Retrato.cs` e `ServicoRetratos.cs` (aparência no retrato e no JSON),
  `src/Vitral.Core/Aplicacao/Aplicador.cs` (parâmetro `EstadoAparencia? aparencia = null`)
- Test: `tests/Vitral.Core.Tests/AparenciaEstadoTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces:
  - `interface IControleAparencia { void Aplicar(AparenciaPerfil aparencia); }`;
  - `sealed class EstadoAparencia(IControleAparencia controle, ServicoConfig config)` com `AparenciaPerfil Atual()`
    (sem guardado → `new(TemaAparencia.VidroEscuro)`), `void Aplicar(AparenciaPerfil? a)` (nulo = não mexer; grava e
    aplica), `void AoIniciar()` (aplica o atual);
  - `ServicoRetratos(..., Func<AparenciaPerfil>? aparenciaAtual = null)`; `Retrato.Aparencia`.

- [ ] **Step 1: testes**

```csharp
[Fact] Aplicar_grava_e_aplica() // controle falso recebe a aparência; config.Ler().Aparencia igual
[Fact] Nulo_nao_mexe()
[Fact] Sem_guardado_vale_Vidro_escuro()
[Fact] Retrato_guarda_e_devolve_a_aparencia() // aplicar perfil com Grafite; reverter ao retrato → volta à anterior (Claro); Voltar ao original → VidroEscuro
[Fact] Pacote_leva_a_aparencia() // exportar/importar (PacoteTests) com a seção
```

- [ ] **Step 2:** rodar → FAIL.
- [ ] **Step 3:** implementar no molde do `EstadoDoca`/`BarraBaixo` (retrato antigo sem a seção → Vidro escuro, como
  `BarraBaixo ?? Desligada`).
- [ ] **Step 4:** rodar → PASS, suíte inteira verde.
- [ ] **Step 5:** commit `feat(core): aparência no estado, nos retratos e no aplicador`.

### Task 3: Editor da aba (Ui)

**Files:**
- Create: `src/Vitral.Ui/EditorAparenciaViewModel.cs`
- Modify: `src/Vitral.Ui/PrincipalViewModel.cs` (`EditorAparencia`, `Carregar(p?.Aparencia)`, `Aparencia =
  EditorAparencia.ParaPerfil()` no perfil salvo)
- Test: `tests/Vitral.Ui.Tests/EditorAparenciaTests.cs`

**Interfaces:**
- Consumes: Tasks 1 e 2.
- Produces: `EditorAparenciaViewModel(Func<VisualWindows> windows)`:
  - `TemaAparencia? Tema` (nulo = "Não mexer"), `IReadOnlyList<TemaAparencia?> Temas`;
  - `EditorAjustes Geral, Doca, Notch, Ilhas` (cada um: `MaterialParte? Material`, `string? CorFundo`, `int? Transparencia`,
    `string? CorDestaque`, `CantosParte? Cantos`, e — só Doca/Notch/Ilhas — `bool Diferenciar`);
  - `ValoresAparencia? Previa` (recalculada a cada mudança; nula com "Não mexer");
  - `void Carregar(AparenciaPerfil?)`, `AparenciaPerfil? ParaPerfil()`.

- [ ] **Step 1: testes**

```csharp
[Fact] Nao_mexer_da_nulo()
[Fact] Tema_e_campos_vao_para_o_perfil() // Grafite + Geral.CorDestaque → AparenciaPerfil(Grafite, Geral: (CorDestaque: "#FF8A8A"))
[Fact] Diferenciar_desmarcado_nao_leva_a_parte() // Notch com campos mas Diferenciar false → Notch nulo
[Fact] Carregar_e_ParaPerfil_ida_e_volta()
[Fact] Previa_muda_com_o_tema() // Claro → Previa.Ilhas.Texto escuro
```

- [ ] **Step 2–4:** FAIL → implementar → PASS.
- [ ] **Step 5:** commit `feat(ui): editor da aparência`.

### Task 4: Serviço de aparência, ilhas, painéis e doca (App)

**Files:**
- Create: `src/Vitral.App/Tema/ServicoAparencia.cs`
- Modify: `src/Vitral.App/Barra/VidroIlha.cs` (`Atualizar(janela, casca, ValoresParte)`), `App.xaml.cs` (cria o serviço,
  `EstadoAparencia`, passa ao `Aplicador`, aos retratos, ao `PrincipalViewModel` o `Func<VisualWindows>`),
  `JanelaIlha.cs`, `JanelaPainel.cs`, XAML/código das ilhas e painéis, `Doca/JanelaDoca.cs`, `ConteudoDoca.xaml`,
  `JanelaMiniaturas.cs`, `MenuDoca.cs`

**Interfaces:**
- Consumes: Tasks 1 e 2.
- Produces: `ServicoAparencia : IControleAparencia` com `ValoresAparencia Atual`, `event Action? Mudou`,
  `static VisualWindows LerWindows()` (modo escuro: `AppsUseLightTheme`/`SystemUsesLightTheme` em
  `HKCU\...\Themes\Personalize`; destaque: `AccentColorMenu`/DWM; transparência: `EnableTransparency`); publica as
  chaves de recurso (Global Constraints); ouve `UserPreferenceChanged` (General, Color) e recalcula no Automático.

- [ ] **Step 1:** varredura das cores fixas nessas partes (`git grep` por `#`, `Ambar`, `NoiteVidro`, `"Texto"`,
  `TextoSecundario`, `Linha`, `SuperficieHover` nos arquivos listados); trocar por `DynamicResource
  Aparencia.<Parte>.*` (XAML) ou leitura no `Mudou` (código).
- [ ] **Step 2:** `VidroIlha.Atualizar` com `ValoresParte`: vidro → acrílico com `Cor` (ABGR) do fundo; sólido → fundo
  opaco; canto DWM pelo `Cantos`.
- [ ] **Step 3:** build sem avisos; suíte verde; abrir e conferir que o padrão (Vidro escuro) está igual ao de hoje.
- [ ] **Step 4:** commit `feat: aparência nas ilhas, painéis e doca`.

### Task 5: Notch, fichas e botões (App)

**Files:**
- Modify: `src/Vitral.App/Barra/JanelaNotch.cs`, `JanelaNotch.Resposta.cs`, `Pilula.cs`, `PainelPendencias.cs`

**Interfaces:**
- Consumes: `ServicoAparencia` (Task 4).
- Produces: `Pilula.Criar` lê `Aparencia.Notch.*` (spec §5.2: principal com fundo = texto do notch e texto = fundo; contorno
  com borda do texto a 28%; perigo `#FF8A8A` com texto claro, `#C62828` com texto escuro).

- [ ] **Step 1:** corpo do notch (`Corpo`) com o fundo e a opacidade do notch; textos, ponto de lembrete, barra de tempo e
  caixa de resposta com o texto/destaque do notch; refazer no `Mudou`.
- [ ] **Step 2:** `Pilula` como acima (os botões já criados se atualizam no `Mudou`).
- [ ] **Step 3:** build; suíte; conferência rápida com fichas simuladas (`pedido.ps1`, `resposta.ps1`) em Vidro escuro.
- [ ] **Step 4:** commit `feat: aparência no notch e nas fichas`.

### Task 6: Aba "Aparência" e prévia (App)

**Files:**
- Create: `src/Vitral.App/PreviaAparencia.cs`
- Modify: `src/Vitral.App/JanelaPrincipal.xaml` (aba entre "Barra do topo" e "Janelas")

**Interfaces:**
- Consumes: `EditorAparenciaViewModel` (Task 3).
- Produces: `PreviaAparencia : FrameworkElement`-based control com `ValoresAparencia? Valores` (DependencyProperty) que
  desenha ilhas, notch e doca sobre o papel de parede (`HKCU\Control Panel\Desktop\WallPaper`; ilegível → gradiente
  escuro), com `BlurEffect` sob as partes em vidro.

- [ ] **Step 1:** aba conforme `aba-aparencia.html`: grade de temas com miniatura (PreviaAparencia pequena), prévia
  grande, personalizar (material, fundo, transparência, destaque, cantos, todos "do tema") e diferenciar.
- [ ] **Step 2:** build; suíte; conferência com o Gustavo: os sete temas na prévia e aplicados (Testar 20 s), o
  diferenciar, o tema Claro item por item (Review Focus 1).
- [ ] **Step 3:** commit `feat: aba Aparência com prévia`.

### Task 7: Teste real e fechamento

- [ ] **Step 1:** com o Gustavo:
  - Automático com troca do modo do Windows;
  - transparência do Windows desligada;
  - Desfazer;
  - Voltar ao original;
  - exportar e importar.
- [ ] **Step 2:** revisão final por um agente Opus com o Review Focus; Crítico e Importante com teste primeiro.
- [ ] **Step 3:** com o ok do Gustavo:
  - versão `0.10.0` e `docs/notas/0.10.0.md`;
  - merge na main e push;
  - `build.ps1 -Versao 0.10.0 -Publicar`;
  - atualizar `docs/ideias-pendentes.md`.
