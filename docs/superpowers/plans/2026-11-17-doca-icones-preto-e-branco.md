# Ícones da doca em preto e branco: plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** opção por perfil "Cor dos ícones: Originais | Preto e branco" na doca, com volta à cor no hover.

**Architecture:** o perfil ganha `FormasDoca.CorIcones` (enum), resolvido como as outras formas da doca. A conta do
cinza é uma função pura no Core sobre pixels BGRA pré-multiplicados. No App, o `ServicoFormas` expõe se os ícones
estão em cinza (com aviso de mudança) e um conversor gera e guarda a cópia cinza de cada ícone; a doca põe a cópia
por cima do original e a apaga no hover.

**Tech Stack:** .NET 10, WPF, CommunityToolkit.Mvvm, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-17-doca-icones-preto-e-branco-design.md`

## Global Constraints

- Português no código, comentários e textos da UI.
- Nulo no perfil = "Não mexer"; padrão resolvido = `Originais`.
- Valor desconhecido no JSON vira nulo (já garantido pelo `EnumTolerante` para enums anuláveis); não sobe o esquema.
- Conversão uma vez por instância de ícone, guardada em cache fraco; nunca por quadro.
- Falha na conversão devolve o ícone original.
- Testes: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet build --nologo -v q; dotnet test --nologo -v q --no-build'`.
- Commits terminam com `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- Pixel totalmente transparente (alfa 0) continua 0,0,0,0; pixel semitransparente não ganha halo (testado na Tarefa 2).
- Trocar de perfil com a doca aberta (Preto e branco → Originais) reflete na hora, sem reabrir (Tarefa 4, teste na máquina).
- Ícone que não é `BitmapSource` (ex.: `DrawingImage`) ou de tamanho zero: o conversor não lança; devolve o original (Tarefa 4).
- App sem ícone (`Icone` nulo, mostra o glifo `SemIcone`): a camada cinza fica vazia, nada quebra (Tarefa 4).
- Ícone de janela trocado pelo app (nova instância): a cópia nova é gerada, a antiga é liberada com o ícone (cache fraco, Tarefa 4).

---

### Task 1: Opção no perfil e nas formas resolvidas

**Files:**
- Modify: `src/Vitral.Core/Perfis/FormasPerfil.cs`
- Modify: `src/Vitral.Core/Formas/ResolverFormas.cs`
- Test: `tests/Vitral.Core.Tests/FormasTests.cs`, `tests/Vitral.Core.Tests/PerfilJsonTests.cs`

**Interfaces:**
- Produces: `public enum CorIconesDoca { Originais, PretoEBranco }`; `FormasDoca(FormatoDoca? Formato = null, TamanhoIcones? Icones = null, bool? Ampliacao = null, CorIconesDoca? CorIcones = null)`; `ValoresDoca(..., bool CantosRetos, CorIconesDoca CorIcones = CorIconesDoca.Originais)` (último parâmetro, com padrão, para não mexer nas chamadas existentes).

- [ ] **Step 1: testes que falham**
  - `FormasTests.Cor_dos_icones_padrao_e_originais`: `R(null).Doca.CorIcones == CorIconesDoca.Originais`.
  - `FormasTests.Cor_dos_icones_preto_e_branco`: `R(new FormasPerfil(new FormasDoca(CorIcones: CorIconesDoca.PretoEBranco))).Doca.CorIcones == PretoEBranco`.
  - `FormasTests.Mesclar_mantem_cor_dos_icones_de_baixo`: embaixo `CorIcones: PretoEBranco`, cima `new FormasDoca(Formato: Corrida)` → mesclado tem `CorIcones == PretoEBranco` e `Formato == Corrida`; e cima com `CorIcones: Originais` sobrepõe.
  - `PerfilJsonTests.Cor_dos_icones_ida_e_volta`: perfil com `Formas.Doca.CorIcones = PretoEBranco` → `Escrever` contém `"corIcones":"pretoEBranco"` e `Ler(Escrever(p))` devolve o mesmo valor.
  - `PerfilJsonTests.Cor_dos_icones_desconhecida_vira_nulo`: JSON com `"corIcones":"umTom"` lê com `CorIcones == null` e o resto do perfil intacto (montar a partir do exemplo usado em `Le_o_exemplo_da_spec`).
- [ ] **Step 2:** rodar; falham por não compilar.
- [ ] **Step 3:** implementar o enum, o campo em `FormasDoca`, o campo em `ValoresDoca`, `Mesclar` (`d.CorIcones ?? embaixo?.Doca?.CorIcones`) e `Doca(f)` (`f?.CorIcones ?? Originais`).
- [ ] **Step 4:** rodar a suíte; tudo verde (o `Tudo_nulo_da_as_medidas_de_hoje` continua passando pelo padrão).
- [ ] **Step 5:** commit `feat: cor dos ícones da doca no perfil`.

### Task 2: A conta do cinza

**Files:**
- Create: `src/Vitral.Core/Formas/TonsDeCinza.cs`
- Test: `tests/Vitral.Core.Tests/TonsDeCinzaTests.cs`

**Interfaces:**
- Produces: `public static class TonsDeCinza { public static void Converter(Span<byte> bgra); }` — pixels BGRA de 32 bits pré-multiplicados, convertidos no lugar; comprimento não múltiplo de 4 lança `ArgumentException`.

- [ ] **Step 1: testes que falham** (`TonsDeCinzaTests`)
  - `Vermelho_puro_opaco`: `[0,0,255,255]` → `[76,76,76,255]` (0,299·255 = 76,2 → 76).
  - `Verde_e_azul_puros`: `[0,255,0,255]` → cinza 150; `[255,0,0,255]` → cinza 29.
  - `Branco_e_preto_nao_mudam`: `[255,255,255,255]` e `[0,0,0,255]` ficam iguais.
  - `Transparente_continua_transparente`: `[0,0,0,0]` fica `[0,0,0,0]`.
  - `Semitransparente_mantem_alfa_e_nao_passa_do_alfa`: vermelho pré-multiplicado meio transparente `[0,0,128,128]` → `[38,38,38,128]`, e em geral cada canal ≤ alfa.
  - `Varios_pixels`: dois pixels no mesmo span são convertidos cada um.
  - `Tamanho_invalido_lanca`: span de 6 bytes → `ArgumentException`.
- [ ] **Step 2:** rodar; falham.
- [ ] **Step 3:** implementar. Conta em inteiros: `cinza = (299*R + 587*G + 114*B + 500) / 1000`, recortado ao alfa; alfa intacto.
- [ ] **Step 4:** rodar; verde.
- [ ] **Step 5:** commit `feat: conta dos tons de cinza para os ícones`.

### Task 3: A opção no editor do perfil

**Files:**
- Modify: `src/Vitral.Ui/EditorFormasViewModel.cs`, `src/Vitral.Ui/Opcoes.cs`
- Test: `tests/Vitral.Ui.Tests/EditorFormasTests.cs`

**Interfaces:**
- Consumes: `CorIconesDoca`, `FormasDoca.CorIcones` (Tarefa 1).
- Produces: `EditorFormasViewModel.CorIconesDoca` (`CorIconesDoca?`, observável); `Opcoes.CoresIconesDoca` = `[("Não mexer", Nulo), ("Originais", Originais), ("Preto e branco", PretoEBranco)]`.

- [ ] **Step 1: testes que falham**
  - `Cor_dos_icones_ida_e_volta`: `Carregar` de um perfil com `CorIcones = PretoEBranco` → `CorIconesDoca == PretoEBranco` e `ParaPerfil().Doca.CorIcones == PretoEBranco`.
  - `So_cor_dos_icones_gera_secao_da_doca`: só `CorIconesDoca = PretoEBranco` escolhido → `ParaPerfil()` não nulo, `Doca` com os outros campos nulos.
  - `Previa_reflete_cor_dos_icones`: escolher `PretoEBranco` → `Previa.Doca.CorIcones == PretoEBranco`.
  - Incluir `Opcoes.CoresIconesDoca` na lista do teste existente da linha 55 (todas começam com "Não mexer").
- [ ] **Step 2:** rodar; falham.
- [ ] **Step 3:** implementar o campo (Carregar, ParaPerfil com a condição de nulo incluindo o novo campo) e a lista de opções.
- [ ] **Step 4:** rodar; verde.
- [ ] **Step 5:** commit `feat: cor dos ícones da doca no editor do perfil`.

### Task 4: A doca desenha em cinza (App)

**Files:**
- Modify: `src/Vitral.App/Tema/ServicoFormas.cs`
- Create: `src/Vitral.App/Doca/IconeCinza.cs`
- Modify: `src/Vitral.App/Doca/ConteudoDoca.xaml`, `src/Vitral.App/JanelaPrincipal.xaml`

**Interfaces:**
- Consumes: `ValoresDoca.CorIcones` (Tarefa 1), `TonsDeCinza.Converter` (Tarefa 2), `EditorFormasViewModel.CorIconesDoca` e `Opcoes.CoresIconesDoca` (Tarefa 3).
- Produces: `ServicoFormas : INotifyPropertyChanged` com `public bool IconesCinza { get; }` (true quando `Atual.Doca.CorIcones == PretoEBranco`), avisando a mudança em `Recalcular`; `sealed class IconeCinza : IMultiValueConverter` (valores: `[Icone, IconesCinza]`; devolve `null` quando `IconesCinza` é false ou o ícone é nulo; senão a cópia cinza).

- [ ] **Step 1:** `ServicoFormas`: implementar `IconesCinza` + `PropertyChanged` disparado em `Recalcular` quando o valor muda.
- [ ] **Step 2:** `IconeCinza`: `ConditionalWeakTable<ImageSource, BitmapSource>` estático. Para gerar: se não for `BitmapSource`, rasterizar com `DrawingVisual` + `RenderTargetBitmap` no tamanho do ícone (Pbgra32); se for, `FormatConvertedBitmap` para `Pbgra32`; `CopyPixels` para `byte[]`, `TonsDeCinza.Converter`, `BitmapSource.Create(..., PixelFormats.Pbgra32, ...)` com o mesmo DPI, `Freeze()`. Tamanho zero ou qualquer exceção (exceto `OutOfMemoryException`) → registrar com `LogBarra.Erro("IconeCinza", e)` e devolver o original. `ConvertBack` → `NotSupportedException`.
- [ ] **Step 3:** `ConteudoDoca.xaml`: recurso `<local:IconeCinza x:Key="ParaCinza" />`. No `Grid` do ícone, logo após a `Image` original, uma segunda `Image x:Name="Cinza"` com o mesmo tamanho/alinhamento/`BitmapScalingMode` e `Source` por `MultiBinding` (`Icone` e `{Binding IconesCinza, Source={x:Static tema:ServicoFormas.Instancia}}`) — `ServicoFormas` é `internal`; se o `x:Static` não aceitar, tornar a classe `public` ou expor por um `ObjectDataProvider`/propriedade estática pública. Nos `DataTemplate.Triggers`, um `Trigger Property="IsMouseOver" SourceName="Raiz"` com EnterActions animando `Cinza.Opacity` para 0 em 0:0:0.15 e ExitActions de volta a 1 em 0:0:0.15.
- [ ] **Step 4:** `JanelaPrincipal.xaml`: no cartão "Formas da doca", nova linha (Grid.Row="3", com `RowDefinition` a mais) "Cor dos ícones" com `ListBox Style="{StaticResource SeletorPilula}" ItemsSource="{x:Static ui:Opcoes.CoresIconesDoca}" SelectedValue="{Binding CorIconesDoca, Converter={StaticResource NuloComoMarca}}"`, margem `0,12,0,0` como as outras.
- [ ] **Step 5:** build + suíte inteira; verde.
- [ ] **Step 6:** commit `feat: ícones da doca em preto e branco`.
- [ ] **Step 7: teste na máquina** (combinar com o Gustavo antes, instância única): fechar o Vitral instalado, abrir o de desenvolvimento, aplicar um perfil com Preto e branco; conferir cinza nos fixados e abertos, bordas lisas no tema escuro e no claro, hover volta à cor, lupa ampliando sem engasgo, pulso âmbar visível, trocar para Originais volta na hora. Fechar o de desenvolvimento e reabrir `%LOCALAPPDATA%\VitralApp\current\Vitral.exe`.
