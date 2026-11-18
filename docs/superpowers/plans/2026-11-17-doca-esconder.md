# Doca escondendo automaticamente — plano de implementação

> **Para agentes:** use superpowers:executing-plans (execução nativa nesta sessão, revisão final por um agente Opus).
> Passos com caixas (`- [ ]`).

**Objetivo:** opção por perfil "Esconder automaticamente" que solta a reserva da doca e a mostra só quando o mouse
encosta na borda de baixo do monitor principal.

**Arquitetura:** o perfil ganha `EsconderAutomatico`; o `EstadoDoca` resolve o "não mexer" e repassa ao
`IControleDoca`. Uma máquina de estados pura em `Vitral.Ui.Doca` decide mostrar/esconder a partir do tempo, do cursor e
das travas; o `ControleDoca` (App) lê o cursor por timer, libera/registra a reserva e anima a janela.

**Stack:** .NET 10, WPF, xUnit, CommunityToolkit.Mvvm.

**Spec:** `docs/superpowers/specs/2026-11-17-doca-esconder-design.md`

## Restrições globais

- Aparecer: cursor 100 ms seguidos na faixa. Sumir: 500 ms seguidos sem cursor na doca/faixa e sem trava.
- Faixa de ativação: os 2 px de baixo do monitor principal, largura toda.
- Animação ~150 ms; com `Movimento.Ligado == false`, direto.
- Tela cheia ganha da borda. Só o monitor principal.
- Português no código, comentários e textos; commits com `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Testes: `powershell.exe -NoProfile -Command '. tools/env.ps1; dotnet build --nologo -v q; dotnet test --nologo -v q --no-build'`.

## Foco da revisão

1. Menu do botão direito aberto com o mouse fora da doca: não pode sumir (trava).
2. Arraste de arquivo vindo de uma janela maximizada até a borda: a doca aparece e não some enquanto o arraste dura.
3. Desligar a opção com a doca escondida: aparece e reserva de novo; desligar a doca inteira com ela escondida: nada
   fica preso (timer parado, reserva liberada).
4. Explorer reiniciado / troca de monitor com a opção ligada: `AoRecriar`/`Reajustar` não podem registrar a reserva.
5. Tela cheia entrando com a doca visível por hover, e saindo com a opção ligada: volta escondida, não visível.

---

### Tarefa 1: perfil e estado

**Arquivos:**
- Modificar: `src/Vitral.Core/Perfis/BarraBaixoPerfil.cs`, `src/Vitral.Core/Barra/EstadoDoca.cs`
- Testes: `tests/Vitral.Core.Tests/DocaTests.cs`

**Interfaces:**
- Produz: `public sealed record BarraBaixoPerfil(bool Visivel, bool? EsconderAutomatico = null)`. O
  `IControleDoca.Aplicar` recebe sempre o valor resolvido (`EsconderAutomatico` não nulo quando `Visivel`).

- [ ] Testes (falham primeiro):
  - `Esconder_ida_e_volta_no_json`: `new Perfil(4,"D",null,null, BarraBaixo: new(true, true))` ida e volta igual, e o
    JSON contém `"esconderAutomatico": true`.
  - `Esconder_nulo_mantem_o_aplicado`: aplicar `new(true, true)`, depois `new(true)` → `_doca.Atual()` é
    `new(true, true)` e o último `_controle` recebido é `new(true, true)`.
  - `Primeira_vez_sem_esconder_resolve_para_falso`: aplicar `new(true)` com config vazia → controle recebe `new(true, false)`.
  - `Esconder_muda_sem_religar_a_barra`: aplicar `new(true,false)` e depois `new(true,true)` → controle recebe
    `new(true,true)`; `OcultarAutomaticoAntesDaDoca` guardado não muda.
- [ ] `EstadoDoca.Aplicar`: `resolvido = estado.Visivel ? estado with { EsconderAutomatico = estado.EsconderAutomatico ??
  Atual().EsconderAutomatico ?? false } : estado`; usar `resolvido` no `Ligar` e na gravação. `AoIniciar` também passa o
  `Atual()` (que já tem o valor) para o controle.
- [ ] Rodar os testes; verdes. Commit `feat(doca): perfil com esconder automaticamente`.

### Tarefa 2: editor e aba da doca

**Arquivos:**
- Modificar: `src/Vitral.Ui/EditorDocaViewModel.cs`, `src/Vitral.App/JanelaPrincipal.xaml` (cartão "Doca do Vitral", linha ~232)
- Testes: `tests/Vitral.Ui.Tests/EditorDocaTests.cs`

**Interfaces:**
- Produz: `[ObservableProperty] Escolha _esconder;` e `bool PodeEsconder => Doca == Escolha.Sim` (notificado quando
  `Doca` muda).

- [ ] Testes: `Carregar_e_devolver_ida_e_volta` ganha `new(true, true)` e `new(true, false)`; `Esconder_so_vale_com_a_doca_ligada`:
  `Doca = Nao`, `Esconder = Sim` → `ParaPerfil()` é `BarraBaixoPerfil.Desligada`; `Doca = Sim`, `Esconder = NaoMexer`
  → `new(true, null)`.
- [ ] Implementar; no XAML, segunda linha no cartão: "Esconder automaticamente" com `Opcoes.Escolhas` em
  `SelectedValue="{Binding Esconder}"`, `IsEnabled="{Binding PodeEsconder}"`, e a nota "Some quando o mouse sai e volta
  ao encostar na borda de baixo; as janelas maximizadas usam a tela até embaixo."
- [ ] Testes verdes; commit `feat(doca): opção de esconder na aba da doca`.

### Tarefa 3: máquina de estados

**Arquivos:**
- Criar: `src/Vitral.Ui/Doca/AutoOcultarDoca.cs`
- Testes: `tests/Vitral.Ui.Tests/AutoOcultarDocaTests.cs`

**Interfaces:**
- Produz:
  ```csharp
  public enum AcaoDoca { Nenhuma, Mostrar, Esconder }
  public sealed class AutoOcultarDoca
  {
      public static readonly TimeSpan EsperaAparecer = TimeSpan.FromMilliseconds(100), EsperaSumir = TimeSpan.FromMilliseconds(500);
      public const double FaixaPx = 2;
      public bool Visivel { get; }                       // começa true
      public static bool NaBorda(double x, double y, Retangulo monitor);       // y >= base - 2, x dentro
      public static bool SobreDoca(double x, double y, Retangulo doca, Retangulo monitor); // x na doca, y de doca.Y até a base
      public AcaoDoca Conferir(DateTime agora, bool naBorda, bool sobreDoca, bool travada, bool telaCheia);
      public void Reiniciar(DateTime agora, bool visivel); // ligar a opção, sair da tela cheia
  }
  ```
  (`Retangulo` é `Vitral.Ui.Barra.Retangulo`, em pixels aqui.)

- [ ] Testes:
  - `Some_depois_de_500ms_sem_mouse`: `Reiniciar(t0, true)`; `Conferir(t0+499ms, ...false)` → `Nenhuma`;
    `t0+500ms` → `Esconder`; `Visivel` false.
  - `Mouse_na_doca_adia_o_sumir`: em t0+400 `sobreDoca` true; em t0+800 sem mouse → `Nenhuma`; em t0+1300 → `Esconder`.
  - `Trava_impede_sumir`: `travada` true por 2 s → `Nenhuma`; solta em t, some em t+500.
  - `Aparece_com_100ms_na_borda`: escondida; `naBorda` em t → `Nenhuma`; t+99 → `Nenhuma`; t+100 → `Mostrar`.
  - `Sair_da_borda_zera_a_espera`: borda em t, fora em t+50, borda em t+60 e t+150 → `Nenhuma`; t+160 → `Mostrar`.
  - `Tela_cheia_nao_mostra`: escondida, `naBorda` e `telaCheia` por 1 s → `Nenhuma`.
  - `NaBorda_e_SobreDoca`: monitor (0,0,1920,1080): (500,1078) e (500,1079) na borda, (500,1077) não; doca
    (700,1000,520,54): (710,1020) e (710,1079) sobre, (690,1020) não.
- [ ] Implementar; verdes; commit `feat(doca): regra de esconder e aparecer`.

### Tarefa 4: a doca de verdade

**Arquivos:**
- Modificar: `src/Vitral.App/Doca/ControleDoca.cs`, `src/Vitral.App/Doca/ConteudoDoca.xaml.cs`,
  `src/Vitral.App/Doca/MenuDoca.cs` (se preciso para saber se o menu está aberto)

**Interfaces:**
- Consome: `BarraBaixoPerfil.EsconderAutomatico` (Tarefa 1), `AutoOcultarDoca` (Tarefa 3).
- Produz: `ConteudoDoca.Ocupada` (`bool`): menu da doca aberto, miniaturas visíveis ou arraste (de botão ou de arquivo)
  em andamento.

- [ ] `ControleDoca.Aplicar`: guarda `_esconder`. Ligado: `_reserva.Liberar()` (não `Registrar`), `AutoOcultarDoca.Reiniciar(agora, true)`,
  timer de 50 ms ligado. Desligado: timer parado, `Registrar()`, janela mostrada e posicionada. `AoRecriar`/`Reajustar`
  só registram/reposicionam a reserva com `_esconder == false`. `Fechar` para o timer.
- [ ] Tick: `GetCursorPos` (pixels), `MonitorPrincipal.Ler()`, retângulo da doca em pixels pela posição visível
  (`PosicaoIlhas.Doca` × DPI); chama `Conferir(..., travada: conteudo.Ocupada, telaCheia: _telaCheia)`; `Mostrar` →
  `Show()` na posição escondida e desliza até a visível; `Esconder` → desliza até o topo da janela ficar na base do
  monitor e `Hide()`. Deslize por `PosicaoNativa.Colocar` em quadros de ~16 ms por 150 ms (ease-out); com
  `Movimento.Ligado` false, salta direto. `Posicionar()` com a doca escondida não a mostra.
- [ ] `AoMudarTelaCheia`: com `_esconder`, ao sair da tela cheia fica escondida (`Reiniciar(agora, false)`), não `Show()`.
- [ ] Build e testes verdes; commit `feat(doca): esconder e aparecer pela borda`.
- [ ] Teste na máquina (combinar com o Gustavo; instância única): fechar o instalado, abrir o de desenvolvimento,
  conferir os cinco itens do Foco da revisão e uma janela maximizada indo até embaixo; fechar o de desenvolvimento e
  reabrir `%LOCALAPPDATA%\VitralApp\current\Vitral.exe`.

### Tarefa 5: fechamento

- [ ] Revisão final por agente Opus no branch inteiro; corrigir Critical/Important; minors em `docs/ideias-pendentes.md`.
- [ ] Notas da versão em `docs/notas/` (versão escolhida depois de `git fetch` e `gh release list`).
- [ ] Commit; pedir o ok do Gustavo para juntar no main e publicar.
