# Prova da central de notificações e das configurações rápidas (resultado)

Data: 2026-11-01 · Windows 11 Home 25H2, build 26200.9550 · Código descartável em `spike/` (`descobrir-central.ps1`,
`tap/SondaFundos.cpp`)

## Quem desenha cada painel

| Painel | Processo | Observação |
|---|---|---|
| Central de notificações (Win+N): notificações, calendário e os balões | `ShellExperienceHost` | Fica **suspenso** enquanto fechado; a conexão só funciona com o painel aberto |
| Configurações rápidas (Win+A): botões e mídia | `ShellHost` (janela `ControlCenterWindow`) | Conecta a qualquer momento |

Os dois usam `Windows.UI.Xaml`, então a rota do Iniciar (InitializeXamlDiagnosticsEx) conecta aos dois.

## Fundos (todos `AcrylicBrush`, tinta 19,33,56 a 80%)

- Central: `Grid NotificationCenterGrid` (notificações), `Grid CalendarCenterGrid` (calendário), `Border
  ToastBackgroundBorder` (cada balão de notificação que aparece na tela).
- Configurações rápidas: `Grid ControlCenterRegion` (botões) e `Grid MediaTransportControlsRegion` (mídia).

## Problema achado

No `ShellExperienceHost` o componente **não consegue vigiar o Vitral** (`OpenProcess` → erro 5, acesso negado). É
esse vigia que devolve o visual do Windows quando o Vitral sai ou cai. Pintar a central exigiria outro aviso de vida
(por exemplo, o Vitral regravar um arquivo a cada poucos segundos e o componente desfazer quando ele parar).

## Antes de pintar

O Gustavo lembrou que a barra do topo do Vitral já abre painéis próprios (mídia, internet, volume, bateria) e que as
notificações já estão previstas no notch. A pergunta passa a ser como esses painéis do Windows entram na barra do
topo, com a pintura só como complemento.
