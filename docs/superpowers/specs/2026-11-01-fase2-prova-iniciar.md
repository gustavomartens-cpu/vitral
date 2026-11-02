# Fase 2: prova do Iniciar (resultado)

Data: 2026-11-01 · Windows 11 Home 25H2, build 26200.9550 · Código descartável em `spike/` (`tap/IniciarProva.cpp`,
`rodada-iniciar.ps1`) · Dados: `spike/resultados/iniciar.csv`

## Pergunta

Um componente nosso, carregado pela rota externa (InitializeXamlDiagnosticsEx), conecta ao Iniciar redesenhado do 25H2
de forma repetível e troca um pincel dele, desfazendo tudo ao sair?

## Resultado: passou

| Item | Resultado |
|---|---|
| Conexão | **10/10** (1 com o Iniciar já aberto, 9 com o processo do Iniciar reiniciado antes) |
| Tempo até pintar | ~10 ms depois do SetSite; a varredura de portas não precisou repetir |
| Pincel trocado | Border `AcrylicBorder` (dentro de `StartMenu.StartBlendedFlexFrame`): fundo âmbar e cantos de 20 px, visíveis na tela e mantidos ao fechar e reabrir o Iniciar |
| Desfazer | **10/10**: ao fechar o injector, o componente devolve o pincel e os cantos originais |
| Estabilidade | Nenhuma queda do `StartMenuExperienceHost` (sem eventos 1000/1002); o processo seguiu respondendo |

## O que a prova pediu

- A pasta do componente precisa de leitura para `ALL APPLICATION PACKAGES` (S-1-15-2-1), e a de log, de escrita: o
  Iniciar roda isolado.
- O evento `Local\VitralTapReady-<pid>` do injector não é visto pelo Iniciar; a confirmação veio pelo log.
- Elementos úteis do fundo: `AcrylicBorder` (o vidro), `AcrylicOverlay`, `MainMenuHighContrastBorder`, `StartDropShadow`.

## Próximo passo

O desenho do produto (abordagem C): o componente vira peça do Vitral, lê a aparência do perfil e, quando não conectar,
o Vitral cai nas opções nativas e avisa.
