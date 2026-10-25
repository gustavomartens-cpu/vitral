# Barra de tarefas: botões, comportamento, ícones e relógio

Data: 25/10/2026. Fase da barra de baixo (a do Windows), depois da barra do topo v2 (0.3.0).

## 1. Objetivo

O perfil passa a ajustar a barra de tarefas do Windows:
- os botões e o comportamento dela;
- quais ícones de apps ficam à mostra perto do relógio.

Com a barra do topo ligada, o relógio de baixo some, para não aparecer duplicado. Tudo entra nos retratos, então
Testar, Voltar, Desfazer e Original desfazem também o que esta fase mudar.

## 2. Decisões e limites (do teste de 25/10)

1. **Sem código dentro do Explorer** (decisão antiga). Só configurações do próprio Windows em `HKCU`.
2. **Relógio de baixo:** existe opção por usuário e ela vale na hora
   (`Explorer\Advanced\ShowSystrayDateTimeValueName = 0`). Fica no lugar dele o sininho de notificações.
3. **Volume, rede e bateria de baixo continuam.** Escondê-los só é possível com políticas de administrador: o Windows
   negou a escrita em `HKCU\...\Policies\Explorer` sem elevação. Além disso, no Windows 11 os três são um botão só.
   Esses três seguem duplicados: embaixo o botão do Windows, em cima os painéis do Vitral.
4. **A transparência fica com o TranslucentTB.** `SetWindowCompositionAttribute` aplicado por fora na janela da barra
   não muda nada no 25H2, porque o fundo é desenhado por dentro do Explorer.
5. **Ícones perto do relógio:** `Control Panel\NotifyIconSettings\<id>\IsPromoted` vale na hora. O perfil guarda a
   **lista dos programas à mostra**, pelo nome do executável (`Spotify.exe`), sem diferenciar maiúsculas. Todos os
   outros vão para a setinha ^, inclusive os que aparecerem depois, enquanto o Vitral estiver aberto.
6. **Widgets ficam de fora:** o Windows bloqueia a escrita de `TaskbarDa` (UCPD). O botão de Copilot não existe
   mais no 25H2.

## 3. Perfil (esquema 3)

```json
"barraTarefas": {
  "pesquisa": "icone",
  "visaoTarefas": false,
  "mostrarAreaDeTrabalho": true,
  "agrupar": "sempre",
  "ocultarAutomaticamente": false,
  "todosOsMonitores": false,
  "selos": true,
  "piscar": true,
  "iconesVisiveis": ["Spotify.exe", "WhatsApp.exe"]
}
```

- Campo ausente ou `null` = não mexer. Seção ausente ou `null` = não mexer em nada.
- `iconesVisiveis: []` esconde todos os ícones de apps.
- `pesquisa`: `oculta` | `icone` | `caixa`. `agrupar`: `sempre` | `quandoCheia` | `nunca`.
- **Migração:** perfis de esquema 1 e 2 são lidos com `barraTarefas = null` e gravados como esquema 3. Pacotes
  `.vitral` antigos continuam importáveis.
- O alinhamento da barra continua em `nativo.alinhamentoBarra`.

## 4. Valores no registro

Todos em `HKCU\Software\Microsoft\Windows\CurrentVersion\…`:

| Opção | Chave e valor | Gravado |
|---|---|---|
| Pesquisa | `Search\SearchboxTaskbarMode` | 0 oculta, 1 ícone, 2 caixa |
| Visão de tarefas | `Explorer\Advanced\ShowTaskViewButton` | 0/1 |
| Mostrar área de trabalho | `Explorer\Advanced\TaskbarSd` | 0/1 |
| Agrupar | `Explorer\Advanced\TaskbarGlomLevel` | 0 sempre, 1 quando cheia, 2 nunca |
| Todos os monitores | `Explorer\Advanced\MMTaskbarEnabled` | 0/1 |
| Selos | `Explorer\Advanced\TaskbarBadges` | 0/1 |
| Piscar | `Explorer\Advanced\TaskbarFlashing` | 0/1 |
| Ocultar automaticamente | `Explorer\StuckRects3\Settings` (binário), byte 8: bit `0x01` | o resto dos bytes preservado |
| Relógio de baixo (automático) | `Explorer\Advanced\ShowSystrayDateTimeValueName` | 0 com a barra do topo ligada; apagado com ela desligada |
| Ícones | `HKCU\Control Panel\NotifyIconSettings\<id>\IsPromoted` | 1 se o `ExecutablePath` está na lista, 0 se não |

Para valer na hora:
- Depois de gravar, o Vitral avisa o sistema (`WM_SETTINGCHANGE "TraySettings"`).
- Ocultar automaticamente também chama `SHAppBarMessage(ABM_SETSTATE)`. O Explorer regrava o `StuckRects3` ao sair,
  então o aviso é o que realmente muda o estado.
- A primeira tarefa do plano confirma, opção por opção, o que vale na hora. As opções que exigirem reiniciar o
  Explorer mostram "Vale depois de reiniciar o Explorer" no editor. O Vitral não reinicia o Explorer sozinho.

## 5. Retratos e estado

- Os valores fixos da tabela entram nos valores controlados (`AjustesNativos.Controlados`), e os retratos os capturam
  como já fazem.
- **Ícones:** o retrato captura o `IsPromoted` de **todas** as subchaves de `NotifyIconSettings` que existirem no
  momento. Para isso, `IRegistro` ganha `IReadOnlyList<string> SubChaves(string chave)`, com implementação no registro
  real e no em memória.
- **Ocultar automaticamente:**
  - Reverter regrava `StuckRects3\Settings` e chama `ABM_SETSTATE` com o bit do retrato.
  - O aviso passa por `INotificador`, com o método novo `AvisarOcultarAutomatico(bool)`. O CLI também avisa.
- **Relógio de baixo:** sai de `perfil.barraTopo`.
  - Ao aplicar um perfil com `visivel: true`, o valor é gravado como 0.
  - Com `visivel: false`, o valor é apagado.
  - Com `barraTopo` nulo, o relógio não é tocado.
  - Como o valor também é controlado, voltar a um retrato restaura o que havia.
- **Ícones que aparecem depois:** o `config.json` guarda a lista aplicada (`iconesVisiveis`, como já guarda
  `barraTopo`).
  - Enquanto o Vitral está aberto, ele observa `NotifyIconSettings` (`RegNotifyChangeKeyValue`).
  - Toda subchave nova sem `IsPromoted` recebe o valor pela lista.
  - Com "não mexer" aplicado, a lista guardada é nula e o Vitral não observa nada.

## 6. Tela

A aba "Barra de tarefas" do editor deixa de estar desabilitada. Ela tem três cartões no estilo das outras abas,
todos com "Não mexer":
- **Botões:**
  - Pesquisa: pílulas Oculta / Ícone / Caixa.
  - Visão de tarefas.
  - Mostrar área de trabalho no canto.
- **Comportamento:**
  - Agrupar botões: Sempre / Quando cheia / Nunca.
  - Ocultar automaticamente.
  - Barra em todos os monitores.
  - Selos nos ícones.
  - Piscar ao pedir atenção.
- **Ícones perto do relógio:**
  - Pílulas Não mexer / Escolher.
  - Com "Escolher", a lista dos programas que já apareceram na bandeja: nome do programa (a dica do ícone, ou o nome
    do `.exe`) com uma caixa de marcar. Marcado = à mostra.
  - Um programa que está no perfil mas não existe neste PC aparece na lista como "(não encontrado aqui)" e continua
    marcado.

Rodapé da aba: "Transparência da barra: TranslucentTB. Com a barra do topo ligada, o relógio daqui some."

## 7. Erros

- **Valor bloqueado pelo Windows (UCPD):** usa o mecanismo que já existe. Entra em "não aplicados", e o aviso de
  aplicação parcial diz o nome.
- **`StuckRects3` ausente ou com tamanho inesperado:** "ocultar automaticamente" não é aplicado e entra em "não
  aplicados".
- **`NotifyIconSettings` ilegível:** os ícones não são aplicados. O resto continua, e o erro vai para o log.
- **Observador de ícones com falha:** registra no log e para de observar. O app continua.

## 8. Testes

- **Automáticos (xUnit):**
  - `PerfilJson`:
    - esquema 3 em ida e volta;
    - esquemas 1 e 2 lidos com `barraTarefas = null`;
    - `iconesVisiveis` vazio ≠ ausente.
  - Cálculo dos valores, opção por opção, com os números da tabela §4. Inclui o byte de `StuckRects3` preservando o
    resto.
  - Relógio:
    - barra do topo ligada grava 0;
    - desligada apaga;
    - nula não toca.
  - Ícones:
    - casa pelo nome do executável, sem diferenciar maiúsculas;
    - quem não está na lista recebe 0;
    - lista vazia esconde todos;
    - subchave nova recebe pela lista.
  - Retratos:
    - ícones e `StuckRects3` em ida e volta;
    - reverter avisa `AvisarOcultarAutomatico` com o bit certo.
  - Editor:
    - carregar e salvar a seção;
    - "(não encontrado aqui)";
    - "Não mexer" vira `null`.
- **Manuais, com o Gustavo na tela:**
  - cada opção mudando a barra;
  - Testar e Voltar;
  - ícones (abrir um app novo com ícone de bandeja enquanto o Vitral está aberto);
  - relógio sumindo e voltando com a barra do topo.

## 9. Ordem

1. Teste: o que vale na hora com `TraySettings`, opção por opção. Resultado anotado no plano.
2. Core: esquema 3, cálculo dos valores, relógio, `SubChaves`, retratos.
3. Ícones: aplicar pela lista e observador de ícones novos.
4. Editor: aba "Barra de tarefas".
5. Revisão final e instalador 0.4.0.

Um plano só.
