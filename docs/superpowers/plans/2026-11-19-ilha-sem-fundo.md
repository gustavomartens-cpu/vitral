# Ilha sem fundo — plano

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Terceira disposição das ilhas, "Sem fundo": a ilha de status sem vidro, sem borda e sem faixa atrás, com sombra
suave no texto e nos ícones.

**Architecture:** As regras (quando fica sem vidro, quando tem faixa atrás, quando tem sombra e de que cor) ficam puras em
`Vitral.Ui/Barra/PosicaoIlhas.cs`, testadas. O App só aplica: `ControleBarra.Posicionar` consulta as regras, `VidroIlha`
reaproveita o "sem vidro" da barra única e liga um `DropShadowEffect` na casca.

**Tech Stack:** .NET 10, WPF, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-19-ilha-sem-fundo-design.md`

## Global Constraints

- Português no código, nos comentários e nos documentos.
- Rótulo exato da opção: `"Sem fundo"`; valor do enum: `DisposicaoIlhas.SemFundo`, no fim do enum.
- Sombra: sem deslocamento (`ShadowDepth = 0`), raio 6, opacidade 0,6 (ajustáveis no teste na máquina).
- Testes: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet build --nologo -v q; dotnet test --nologo -v q --no-build'`.
- Commits terminam com `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- Trocar de "Barra única" para "Sem fundo" (e de volta para "Separadas") com o Vitral aberto: a faixa some, a borda da
  casca volta ao sair, a sombra some fora do "Sem fundo". Conferido na máquina (Task 2).
- Trocar o tema (escuro ↔ claro) no modo "Sem fundo": a cor da sombra acompanha (refeita em `VidroIlha.Atualizar`).
- Perfil antigo sem o campo / config com "barraUnica": continua igual (enum no fim, JSON em texto). Teste na Task 1.
- Hover dos itens sobre o papel de parede: a pílula clarinha aparece e some; clique abre o painel. Conferido na máquina.
- "Encostadas" + "Sem fundo": posição igual à das ilhas encostadas, sem cantos. Teste na Task 1.

---

### Task 1: Regras e opção

**Files:**
- Modify: `src/Vitral.Core/Perfis/FormasPerfil.cs` (enum)
- Modify: `src/Vitral.Ui/Barra/PosicaoIlhas.cs`
- Modify: `src/Vitral.Ui/Opcoes.cs:94`
- Test: `tests/Vitral.Ui.Tests/PosicaoIlhasTests.cs`, `tests/Vitral.Ui.Tests/EditorFormasTests.cs:58`, `tests/Vitral.Core.Tests/FormasTests.cs`

**Interfaces:**
- Produces:
  - `DisposicaoIlhas.SemFundo`
  - `PosicaoIlhas.SemVidro(DisposicaoIlhas d) -> bool` (BarraUnica ou SemFundo)
  - `PosicaoIlhas.ComSombra(DisposicaoIlhas d) -> bool` (só SemFundo)
  - `PosicaoIlhas.CorSombra(double luminanciaTexto) -> (byte R, byte G, byte B)`: preto se luminância > 0,5 (texto claro), branco senão
  - `ComFundo(bool barraUnica, bool temStatus)` não muda.

- [ ] **Step 1: Testes que falham**
  - `Sem_fundo_fica_sem_vidro_sem_faixa_e_com_sombra`: `SemVidro(SemFundo)` e `SemVidro(BarraUnica)` verdadeiros, `SemVidro(Separadas)` falso; `ComSombra(SemFundo)` verdadeiro, `ComSombra(BarraUnica)` e `ComSombra(Separadas)` falsos.
  - `Sombra_contraria_ao_texto`: `CorSombra(1.0) == (0,0,0)`, `CorSombra(0.02) == (255,255,255)`.
  - `Sem_fundo_encostada_na_posicao_das_encostadas`: com `new FormasIlhas(SemFundo, Encostadas)`, `Calcular(Tela, 300, 140, 360, …)` dá o status em `(1560, 0, 360, 30)` e `Ilhas.CantosRetos` verdadeiro.
  - `EditorFormasTests`: rótulos `["Não mexer", "Separadas", "Barra única", "Sem fundo"]`.
  - `FormasTests`: `Mesclar(new(Ilhas: new(SemFundo)), new(Ilhas: new(Distancia: Encostadas)))` mantém `SemFundo`; um perfil JSON com `"disposicao": "semFundo"` lê `SemFundo` e um com `"barraUnica"` continua lendo `BarraUnica` (pelo mesmo caminho de leitura que os testes de perfil já usam).
- [ ] **Step 2: Rodar e ver falhar** (não compila: `SemFundo` não existe).
- [ ] **Step 3: Implementar** enum, as três funções em `PosicaoIlhas` e a opção em `Opcoes.DisposicoesIlhas`.
- [ ] **Step 4: Rodar os testes: todos verdes.**
- [ ] **Step 5: Commit** `feat: disposição "Sem fundo" das ilhas (regras e opção)`.

### Task 2: Aplicar na barra e na prévia

**Files:**
- Modify: `src/Vitral.App/Barra/VidroIlha.cs` (propriedade anexada `Sombra`, aplicada em `Atualizar`)
- Modify: `src/Vitral.App/Barra/JanelaIlha.cs` (`AplicarSombra(bool)`, no molde de `AplicarSemVidro`)
- Modify: `src/Vitral.App/Barra/ControleBarra.cs:486-494`
- Modify: `src/Vitral.App/PreviaAparencia.cs:143`

**Interfaces:**
- Consumes: `PosicaoIlhas.SemVidro`, `ComSombra`, `CorSombra`.

- [ ] **Step 1: `VidroIlha`**: `DefinirSombra(Window, bool)` / `Sombra(Window)`. Em `Atualizar`, ao fim do ramo sem vidro:
  com sombra, `casca.Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = 6, Opacity = 0.6, Color = CorSombra(Luminancia(v.Texto)) }`;
  sem sombra (e no ramo com vidro), `casca.Effect = null`.
- [ ] **Step 2: `JanelaIlha.AplicarSombra(bool)`**: só refaz quando muda.
- [ ] **Step 3: `ControleBarra.Posicionar`**: `AplicarSemVidro(PosicaoIlhas.SemVidro(d))`, `AplicarSombra(PosicaoIlhas.ComSombra(d))`; `ComFundo` segue recebendo `d == BarraUnica`.
- [ ] **Step 4: `PreviaAparencia`**: no `SemFundo`, nenhuma pílula de ilha; três marquinhas (círculos de ~2,5 px de raio na escala da prévia) na cor do texto das ilhas, alinhadas à direita onde fica a ilha da direita.
- [ ] **Step 5: Build e testes verdes.**
- [ ] **Step 6: Commit** `feat: ilha de status sem fundo, com sombra suave`.
- [ ] **Step 7: Teste na máquina** (combinado com o Gustavo: fechar o instalado, abrir o de desenvolvimento): os pontos do Review Focus; ajustar raio/opacidade se preciso; depois fechar o de desenvolvimento e reabrir `%LOCALAPPDATA%\VitralApp\current\Vitral.exe`.

### Task 3: Revisão e documentos

- [ ] Revisão final por um agente Opus no diff do ramo; corrigir Critical/Important; minors em `docs/ideias-pendentes.md`.
- [ ] Notas da versão em `docs/notas/` (versão escolhida na hora de publicar, depois de `git fetch` e `gh release list`).
- [ ] Commit `docs: notas da ilha sem fundo`.
