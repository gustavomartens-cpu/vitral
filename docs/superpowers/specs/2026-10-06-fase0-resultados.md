# Fase 0: resultados e decisão

Datas dos testes: 2026-10-06 · Máquina: Windows 11 Home 25H2, build 26200.9550, 3 monitores · Branch: `fase-0`
Plano: `docs/superpowers/plans/2026-10-06-fase0-prova-de-viabilidade.md` · Spec: `docs/superpowers/specs/2026-10-03-vitral-design.md`

## Decisão

**A fase 0 não passou no critério combinado** (alguma rota com pelo menos 9 de 10 rodadas). **O projeto fica pausado** na decisão "caminho 1": registrar e retomar depois. Nada da fase 1 foi iniciado.

## O que foi comprovado

| Pergunta | Resultado |
|---|---|
| As ferramentas cabem no G: sem admin e sem Visual Studio? | **Sim.** .NET 10.0.401, llvm-mingw 20260922 (clang 23.1.2) e C++/WinRT 2.0.250303.1 em `G:\dev`. A instalação usou 1 MB do C:. |
| O componente nativo compila sem depender de runtime externo? | **Sim.** Só importa DLLs do sistema. CFG ligado. Exporta `DllGetClassObject`, `DllCanUnloadNow` e `VitralHookProc`. Precisou de `#undef GetCurrentTime` (macro do `winbase.h` colide com o C++/WinRT). |
| Um componente nosso consegue deixar a barra transparente sem o TranslucentTB? | **Sim, quando entra.** Encontrou as 3 barras (`frames=3`) e trocou o fundo das três (`fills=3`) em ~5,7 s. O Gustavo confirmou visualmente. |
| A restauração automática funciona? | **Sim.** Ao encerrar o injector, as barras voltaram ao normal em todas as rodadas que tinham funcionado (`RESTAURADO=6`). |
| O executável próprio passa pelo UCPD? | **Parcialmente.** `TaskbarDa` (botão de Widgets) fica **bloqueado** (`UnauthorizedAccessException`). `SearchboxTaskbarMode` e `TaskbarAl` passaram. Nenhum valor mudou. Ver `spike/resultados/ucpd.txt`. |
| O componente entra no Iniciar? | **Não testado.** Pausado antes desse passo. |

## Confiabilidade da entrada (o critério que falhou)

Fonte: `spike/resultados/rotas.csv` e `visual.csv`.

| Rodadas | Rota | Sucessos | Observação |
|---|---|---|---|
| 2 iniciais | externa | 0/2 | versão com defeito: o componente travava esperando a si mesmo (corrigido, commit `f8ec0e6`) |
| 1 autoteste + 10 | externa | 4/11 | sucesso no autoteste e nas rodadas 1 a 3; da 4 em diante, todas falharam |
| 10 | interna | 1/10 | a DLL entrava no Explorer, mas o pedido de conexão respondia `0x80070490` (não encontrado) |
| 5 autoteste | externa, varrendo conexões de 1 a 10.000 (como o Windhawk) | 0/5 | todas responderam `0x80070490` |

Nas falhas da rota externa, o componente nem chegava a ser carregado (não há log daquele Explorer). A causa da resposta "não encontrado" **não foi identificada**. A queda ao longo da sessão (funcionava no começo e depois parou de vez) sugere algo que se acumula ou muda no sistema, mas isso é hipótese, não conclusão.

## O que ficou de aprendizado para quando retomar

1. A técnica funciona na prática neste build. O obstáculo é a **confiabilidade da conexão**, não a pintura da barra.
2. Investigar a causa do "não encontrado" **com o Gustavo acompanhando e aprovando cada passo**, antes de qualquer outra mudança.
3. O alternativo, já desenhado na conversa: o Vitral faz a parte nativa, a barra do topo e o tiling (que não precisam entrar no Explorer) e usa o TranslucentTB ou o Windhawk para a barra de baixo.
4. O Vitral não vai conseguir alterar o botão de Widgets (bloqueio do UCPD).

## Estado da máquina ao pausar

- O Explorer foi reiniciado no fim dos testes: barra normal, sem nada do Vitral carregado.
- O TranslucentTB foi **fechado** para os testes. Para a barra transparente de volta, basta abri-lo pelo Menu Iniciar (a configuração dele não foi alterada).
- Ferramentas mantidas em `G:\dev\dotnet`, `G:\dev\toolchains` e `G:\dev\nuget` (para remover: apagar essas pastas).
- Logs do componente em `spike/out/logs` (ignorados pelo git).
