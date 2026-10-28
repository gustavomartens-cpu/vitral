# Repaginação da janela principal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** janela principal em vidro fumê, com barra lateral só de perfis, engrenagem de Configurações, abas por parte da
tela, abrindo no perfil ativo, e sem as opções que não funcionam.

**Architecture:** o comportamento novo (abrir no ativo, "alterações não salvas", pergunta ao trocar de perfil, mensagem
que some) entra no `PrincipalViewModel` (Vitral.Ui), com testes. O visual é trocado nos `Tokens.xaml` e `Estilos.xaml`
e na própria janela; as Configurações viram uma janela nova que recebe o código que hoje está na `JanelaPrincipal`.

**Tech Stack:** .NET 10, WPF, xUnit, CommunityToolkit.Mvvm.

**Spec:** `docs/superpowers/specs/2026-10-28-interface-design.md`

## Global Constraints

- Cores e medidas da spec §4.2 e §4.3, exatamente; âmbar `#F2B544` só para ação.
- Doca, notch e ilhas não mudam (dicionários da aparência por parte).
- Opção escondida continua no modelo e nos perfis (valor preservado ao salvar).
- Textos em português; "Não mexer" vira "—" com dica "Não mexer".
- Testes: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet test --nologo -v q'`. Matar o Vitral de
  desenvolvimento (e, para testar na tela, o instalado) antes de compilar.

## Review Focus

1. **Trocar de perfil com mudança pendente e escolher Cancelar:** a lista volta a mostrar o perfil anterior e o editor
   continua com as mudanças. Teste `Troca_com_pendencia_e_Cancelar_fica_onde_estava` (Task 1) + conferência na tela
   (Task 3), porque a ListBox pode não refletir a volta.
2. **Testar/Aplicar com mudança pendente:** salvam antes (como hoje) e a linha "Alterações não salvas" some. Teste
   `Aplicar_salva_e_limpa_as_alteracoes` (Task 1).
3. **Perfil ativo apagado fora do Vitral:** abre no primeiro, sem erro. Teste `Abre_no_primeiro_sem_ativo_valido` (Task 1).
4. **Opção escondida com valor no perfil:** salvar outra mudança não apaga o valor. Teste
   `Opcao_escondida_mantem_o_valor_ao_salvar` (Task 1).
5. **Papel de parede claro sob o vidro fumê:** texto legível; se não, tinta a 70% (spec §8). Conferência na Task 2.

---

### Task 1: Comportamento da janela (Ui)

**Files:**
- Modify: `src/Vitral.Ui/PrincipalViewModel.cs`, `src/Vitral.Ui/Abstracoes.cs`
- Test: `tests/Vitral.Ui.Tests/PrincipalTests.cs` (e o falso de `IDialogos` dos testes)

**Interfaces:**
- Produces:
  - `enum RespostaPendente { Salvar, Descartar, Cancelar }`; `IDialogos.PerguntarSalvar(string perfil) → RespostaPendente`
    ("Salvar as mudanças em 'X'?"; o App implementa com MessageBox Sim/Não/Cancelar).
  - `PrincipalViewModel.TemAlteracoes` (bool, observável): o perfil montado pelos editores difere do carregado. Comparação
    pela serialização (`PerfilJson`) do perfil montado, sem `BuildTestada`.
  - Ao construir: `Selecionado` = perfil ativo se existir na lista; senão o primeiro; sem perfis, nulo.
  - Trocar `Selecionado` com `TemAlteracoes`: pergunta; Salvar → salva o anterior e troca; Descartar → troca; Cancelar →
    volta ao anterior, sem recarregar o editor.
  - `Mensagem` some 6 s depois de aparecer (pelo `IRelogio`).

- [ ] **Step 1: testes** (`PrincipalTests`): `Abre_no_perfil_ativo`; `Abre_no_primeiro_sem_ativo_valido`;
  `Sem_perfis_nada_selecionado`; `Editar_liga_as_alteracoes_e_salvar_desliga`; `Descartar_desliga_as_alteracoes`;
  `Aplicar_salva_e_limpa_as_alteracoes`; `Troca_com_pendencia_e_Salvar_grava_o_anterior`;
  `Troca_com_pendencia_e_Descartar_perde_a_mudanca`; `Troca_com_pendencia_e_Cancelar_fica_onde_estava` (Selecionado
  volta, editor mantém o valor editado, `TemAlteracoes` continua true); `Opcao_escondida_mantem_o_valor_ao_salvar`
  (perfil com `OcultarAutomaticamente = Sim`, mudar o modo, salvar → continua Sim); `Mensagem_some_depois_de_6s`.
- [ ] **Step 2:** rodar; falha.
- [ ] **Step 3:** implementar. `TemAlteracoes` recalculado no `PropertyChanged` dos seis editores (e da lista de ícones
  da barra de tarefas) e depois de carregar/salvar.
- [ ] **Step 4:** suíte inteira passa (testes antigos que supunham nada selecionado ao abrir: ajustar a expectativa e
  registrar no ledger).
- [ ] **Step 5:** commit `feat(ui): janela abre no perfil ativo e avisa mudanças não salvas`.

### Task 2: Vidro fumê (cores, material e controles)

**Files:**
- Modify: `src/Vitral.App/Tema/Tokens.xaml`, `src/Vitral.App/Tema/Estilos.xaml`, `src/Vitral.App/Vidro.cs`

**Interfaces:**
- Produces: estilos `BotaoPrimario`, `BotaoVidro` (secundário), `BotaoIcone` (novo, 26×26), `Chave` (novo, ToggleButton
  ou CheckBox em forma de interruptor 34×18), `SeletorPilula` segmentado neutro com "—" para a marca "Não mexer",
  `CartaoVidro`, `AbasVitral`/`AbaVitral`, `ListaPerfis`/`ItemListaPerfil`, `BotaoCor` — todos com os valores da spec §4.3.
- `Vidro.Aplicar(Window)`: acrílico com tinta `#8C0C0C0E` (ABGR), modo escuro do DWM, cantos arredondados; sem
  transparência do Windows, fundo `#0E0E10`.

- [ ] **Step 1:** cores da spec §4.2 em `Tokens.xaml` (as chaves de hoje mudam de valor; nenhuma chave some).
- [ ] **Step 2:** estilos da §4.3 em `Estilos.xaml`. O "—": o `SeletorPilula` mostra o rótulo do item; "Não mexer" passa
  a ser "—" nas listas `Opcoes.*` cujo primeiro item é "Não mexer" (mudar em `Vitral.Ui/Opcoes.cs`, com o teste de
  rótulos ajustado) e a dica vem de um `ToolTip` no item quando o rótulo é "—".
- [ ] **Step 3:** `Vidro.Aplicar` com o material novo.
- [ ] **Step 4:** build sem avisos; suíte; na tela: a janela de hoje já com o vidro fumê e os controles novos, sobre o
  papel de parede (Review Focus 5) e com a transparência do Windows desligada.
- [ ] **Step 5:** commit `feat: vidro fumê na janela principal`.

### Task 3: Estrutura da janela

**Files:**
- Modify: `src/Vitral.App/JanelaPrincipal.xaml`, `src/Vitral.App/JanelaPrincipal.xaml.cs`, `src/Vitral.App/DialogosWpf.cs`

- [ ] **Step 1:** barra lateral (spec §3.1): "PERFIS" com `BotaoIcone` "+" (`NovoCommand`) e "⋯" (ContextMenu com
  Importar, Exportar, Desfazer última mudança, Voltar ao Windows original, ligados aos comandos de hoje); a lista; no pé
  "⚙ Configurações". Largura 220.
- [ ] **Step 2:** cabeçalho (spec §3.3): nome + etiqueta "ativo" (quando `Selecionado == PerfilAtivo`); Testar e Aplicar à
  direita; linha de baixo: "Alterações não salvas · Salvar · Descartar" quando `TemAlteracoes`, senão a `Mensagem`. Sai o
  rodapé.
- [ ] **Step 3:** abas da spec §3.4, na ordem; formas da doca na aba 2, formas do notch e das ilhas na aba 3 (os mesmos
  controles da aba Formas de hoje, ligados ao `EditorFormas`); a prévia da Aparência continua lendo `EditorFormas.Previa`.
  Saem as abas Iniciar, Janelas e Formas.
- [ ] **Step 4:** `DialogosWpf.PerguntarSalvar` (MessageBox Sim/Não/Cancelar → Salvar/Descartar/Cancelar).
- [ ] **Step 5:** build; suíte; na tela: abre no ativo, as quatro abas, o "⋯", a troca com pendência e Cancelar (Review
  Focus 1).
- [ ] **Step 6:** commit `feat: nova organização da janela principal`.

### Task 4: Configurações

**Files:**
- Create: `src/Vitral.App/JanelaConfiguracoes.xaml(.cs)`
- Modify: `src/Vitral.App/JanelaPrincipal.xaml.cs`, `src/Vitral.App/App.xaml.cs`, `src/Vitral.App/Agenda/JanelaContas.xaml`

**Interfaces:**
- Produces: `JanelaConfiguracoes(Inicializacao inicializacao, ServicoAtualizacao atualizacao, string versao, Agenda.ServicoAgendaApp? agenda)`.

- [ ] **Step 1:** janela de 420 com o vidro fumê (spec §3.5): chaves Iniciar com o Windows, Avisos do Claude no notch,
  Agenda no notch + "Contas…"; versão, build e a linha de atualização. O código de cada uma sai da `JanelaPrincipal`
  (mesmo comportamento: ganchos do Claude, inicialização, atualização).
- [ ] **Step 2:** a engrenagem da barra lateral abre a janela (dona: a principal).
- [ ] **Step 3:** `JanelaContas` com o vidro fumê e os controles novos.
- [ ] **Step 4:** build; suíte; na tela: cada chave liga e desliga como antes; atualização mostra o estado.
- [ ] **Step 5:** commit `feat: janela de Configurações`.

### Task 5: Opções que não funcionam

**Files:**
- Modify: `src/Vitral.App/JanelaPrincipal.xaml` (esconder linhas), `docs/ideias-pendentes.md`

- [ ] **Step 1:** num perfil de teste ("Conferência"), para cada opção das abas 1–3: aplicar um valor, conferir o efeito no
  Windows (o valor no registro e o efeito visível) e voltar ao original. Anotar no ledger: opção, valor, efeito esperado,
  efeito observado.
- [ ] **Step 2:** as que não fizerem efeito: a linha inteira sai da aba (`Visibility="Collapsed"`, com um comentário
  XAML "escondida até consertar — ideias-pendentes") e entra em `docs/ideias-pendentes.md`, seção "Opções a consertar",
  com o que foi observado.
- [ ] **Step 3:** mostrar a lista ao Gustavo.
- [ ] **Step 4:** commit `chore: esconde opções que não funcionam`.

### Task 6: Fechamento

- [ ] **Step 1:** conferência geral com o Gustavo (abas, Configurações, "⋯", vidro, teclado).
- [ ] **Step 2:** revisão final por um agente Opus com o Review Focus; Crítico e Importante com teste primeiro.
- [ ] **Step 3:** com o ok do Gustavo: versão `0.13.0` e `docs/notas/0.13.0.md`; merge na main e push;
  `build.ps1 -Versao 0.13.0 -Publicar`; atualizar `docs/ideias-pendentes.md`.
