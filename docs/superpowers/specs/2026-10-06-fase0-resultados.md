# Fase 0: resultados e decisão

Testes: 2026-10-06 (1ª rodada) e 2026-10-24 (investigação e 2ª rodada) · Máquina: Windows 11 Home 25H2, build 26200.9550 · Branch: `fase-0`
Plano: `docs/superpowers/plans/2026-10-06-fase0-prova-de-viabilidade.md` · Spec: `docs/superpowers/specs/2026-10-03-vitral-design.md`

## Decisão

**Segue, com ressalva.** A rota **externa** deixou a barra transparente em **10 de 10** rodadas, com restauração automática em 10 de 10 e sem nenhuma queda do Explorer. **Rota padrão da fase 1: externa.**

**Ressalva (revisão final, 2026-10-24):** o critério do plano pede **3 barras**. As 10/10 foram com **1 barra**, porque a opção "barra em todos os monitores" estava desligada. Com 3 barras só há 4 sucessos, todos de 2026-10-06 e anteriores à correção. **Antes de fechar a fase 1b**, refazer 10 rodadas da rota externa com a opção ligada (o Gustavo liga pelas Configurações).

O componente também conectou ao **Iniciar redesenhado**, mas em **1 tentativa só** pela rota externa; a rota interna não foi testada no Iniciar.

> Histórico: em 2026-10-06 a fase foi dada como "não passou" (4/11 externa, 1/10 interna) e pausada. A investigação de 2026-10-24 encontrou a causa raiz (abaixo); com a correção, o critério foi cumprido.

## Causa raiz das falhas de 2026-10-06

O Windows registra a porta de diagnóstico de cada núcleo de interface XAML (`VisualDiagConnection<n>`, com `n` crescente por processo) **só quando a barra termina de montar a interface** (código público da Microsoft: `DXamlCore::RegisterVisualDiagnosticsPort`). O roteiro tentava a conexão 6 s depois de reiniciar o Explorer. Quando a barra ainda não estava pronta, não havia **nenhuma** porta, e todas as tentativas respondiam `0x80070490` (não encontrado). Na rota interna havia um segundo erro: cada tentativa usava um número diferente, e o número certo (1) só era tentado uma vez, cedo demais.

Teste controlado (modo só-registro): espera de 6 s, 2 de 3 conectaram; espera de 60 s, 3 de 3. Correção (commit `fa96e8f`): a rota externa **repete a varredura a cada 2 s até a porta existir** (limite de 90 s). Os logs da 2ª rodada mostram o padrão: cerca de 6 s de "não encontrado" e depois a conexão na porta 1.

## Resultados

| Pergunta | Resultado |
|---|---|
| Ferramentas no G: sem admin e sem Visual Studio | **Sim.** .NET 10.0.401, llvm-mingw 20260922 (clang 23.1.2), C++/WinRT 2.0.250303.1. A instalação usou 1 MB do C:. |
| Componente sem runtime externo | **Sim.** Só importa DLLs do sistema; CFG ligado. Precisou de `#undef GetCurrentTime`. |
| Barra transparente sem o TranslucentTB, rota externa | **10/10** (Gustavo confirmou visualmente), restauração **10/10**, tempo de 2,5 a 7,9 s |
| Rota interna | conexão 10/10; visual 9/10, restauração 8/9 (ver "pendências") |
| Segunda conexão no mesmo Explorer | **Recusada** (código 2), como esperado |
| TranslucentTB aberto | Injector **recusa** (código 3) sem tocar em nada |
| Explorer reiniciado com o componente carregado | Sobe limpo. Isso aconteceu em cada uma das ~40 rodadas, sem queda |
| Iniciar redesenhado (StartMenuExperienceHost) | **Conectou em 1 tentativa** pela rota externa; o log tem 2.057 linhas `ADD` (`StartMenu.StartBlendedFlexFrame`, `AcrylicBorder`, `StartFrame`...). Rota interna não testada. **Questão aberta para a fase 2:** o evento `Local\` foi visto em 320 ms. Isso contradiz a suposição de que o Iniciar roda isolado em contêiner de app (ou os objetos `Local\` são visíveis entre os dois); investigar antes de desenhar a ACL e o canal da fase 2. |
| Injector morto à força | Coberto pelo mesmo mecanismo da saída normal (`RegisterWaitForSingleObject` no PID); um kill de verdade **não foi exercitado** |
| Travamento real | Em 2026-10-06 21:14, na primeira rodada (versão com o deadlock), o Windows encerrou o Explorer por travamento (`AppHangB1`) |
| UCPD | `TaskbarDa` (Widgets) **bloqueado** até para o nosso exe; `SearchboxTaskbarMode` e `TaskbarAl` passam (`spike/resultados/ucpd.txt`) |
| Monitores | Em 2026-10-24 a opção "barra em todos os monitores" estava desligada (`MMTaskbarEnabled=0`): 1 barra, `frames=1`. Em 2026-10-06, com 3 barras, o componente viu `frames=3`. |

Dados brutos: `spike/resultados/rotas.csv`, `visual.csv`; logs em `spike/out/logs` (fora do git).

## Pendências para a fase 1

1. **O Windows repinta o fundo da barra** (hipótese, não testada): em 2 rodadas da rota interna, o registro diz que a transparência foi aplicada/restaurada, mas o Gustavo viu o contrário. O TranslucentTB trata isso vigiando a troca do `Fill` (`RegisterPropertyChangedCallback`) e reaplicando. Implementar e testar na fase 1.
2. **Esperar a barra ficar pronta** em todo carregamento: no login, após reinício do Explorer e após troca de monitores.
3. Ao conectar num processo, carregar o componente uma vez só e soltá-lo ao sair (no teste do Iniciar ele ficou preso no processo até eu reiniciá-lo).
4. Botão de Widgets fora do alcance do app (UCPD).

### Requisitos para a fase 1b vindos da revisão final (cada um com teste)

1. **Desativar o componente quando o host morre:** flag "ativo" desligada antes de restaurar, `UnadviseVisualTreeChange`, e aplicar e restaurar sob o mesmo lock. Hoje ele restaura mas continua inscrito, e uma barra nova (hot-plug, DPI, barra em todos os monitores) ficaria transparente sem como desfazer.
2. **Confirmar a restauração na thread de UI:** registrar dentro do callback despachado, relendo o `Fill` depois de gravar. `RESTAURADO=n` e `FILL_APLICADO` hoje só dizem que algo foi enfileirado ou chamado. Fazer toda a restauração num único callback na UI, porque liberar objetos XAML fora dela pode derrubar o Explorer.
3. **Restaurar sem quebrar o tema:** guardar `ReadLocalValue(FillProperty)`. Se não era valor local, restaurar com `ClearValue` em vez de gravar o pincel antigo. O mesmo vale para `Opacity`.
4. **Não aplicar sem vigia do host:** se `OpenProcess` ou o registro da espera falhar, não aplicar nada.
5. **Detectar outro consumidor pelos módulos carregados no alvo**, não pelo nome do processo: o TAP de outra ferramenta pode continuar carregado depois que o exe dela fecha.
6. **Reaplicar quando o Windows repinta o `Fill`** (`RegisterPropertyChangedCallback`), com teste de corrida no instante em que a porta aparece. As duas anomalias da rota interna aconteceram justamente nas rodadas que conectaram no momento em que a porta surgiu.
7. Mutex de "já anexado" só depois de o `Advise` dar certo. Log por PID e por sessão, para não confundir com rodadas antigas. Escrita de log fora da thread de UI.

## Erros meus durante a fase (corrigidos)

- O componente esperava o próprio aviso terminar, e travava (deadlock na thread de interface). Corrigido no commit `f8ec0e6`.
- A conexão era tentada cedo demais e, na rota interna, com um número de porta por tentativa (acima).
- O injector quebrava ao ler o registro enquanto o componente escrevia (agora lê em modo compartilhado).

## Estado da máquina

Explorer e Iniciar limpos (sem o componente). **TranslucentTB aberto**, como antes dos testes. As ferramentas continuam em `G:\dev\dotnet`, `G:\dev\toolchains` e `G:\dev\nuget`.
