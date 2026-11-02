# Notificações dos apps no notch

Data: 2026-11-02 · Aprovado na conversa com o Gustavo · Provas: `docs/superpowers/specs/2026-11-01-prova-central.md`
(identidade de pacote e leitura das notificações)

## 1. O que é

As notificações dos outros apps (Chrome, Email, Celular, Defender...) passam a aparecer no notch do Vitral, e a
central do Windows deixa de ser o lugar delas. Substitui a pendência "Notificações de outros apps no notch".

**Sucesso:**
- uma notificação nova desce no notch com o ícone e o nome do app, o título e o texto, e o balão do Windows não
  aparece;
- o sino da lateral direita do notch mostra quantas há e abre o painel do Vitral, agrupado por app;
- clicar abre o app; limpar no Vitral limpa no Windows;
- as notificações do app do Claude ficam de fora (os avisos do Claude vêm só pelos ganchos).

Fora: os botões próprios das notificações (responder, marcar como lida), silenciar app pelo painel, a central do
Windows pintada.

## 2. Identidade de pacote

- Pacote esparso `Vitral` (manifesto solto em `%LOCALAPPDATA%\Vitral\pacote\AppxManifest.xml`), registrado pelo próprio
  Vitral ao abrir com `PackageManager.RegisterPackageByUriAsync(..., ExternalLocationUri = pasta do Vitral.exe,
  DeveloperMode)`, só se ainda não estiver registrado com essa pasta. A pasta `current` do Velopack é a mesma depois de
  cada atualização.
- Capacidades: `runFullTrust`, `unvirtualizedResources`, `userNotificationListener`. `uap10:TrustLevel="mediumIL"` e
  `RuntimeBehavior="win32App"`: o Vitral continua um programa comum (registro e arquivos sem virtualização).
- `src/Vitral.App/app.manifest` ganha `<msix publisher="CN=Vitral" packageName="Vitral" applicationId="Vitral"/>`.
  Sem o pacote registrado, o Windows ignora o elemento e o Vitral roda sem identidade.
- Requer o modo de desenvolvedor do Windows (manifesto solto sem assinatura). Sem ele, ou com o registro recusado:
  estado `SemIdentidade`.
- Permissão: `UserNotificationListener.RequestAccessAsync` na primeira vez; negada → estado `SemPermissao`.

## 3. Leitura

- `ServicoNotificacoes` (App) lê `GetNotificationsAsync(Toast)` e publica uma lista de `NotificacaoApp(Id, AppId,
  NomeApp, Titulo, Texto, Chegou, Icone)`.
- Mudanças: o evento `NotificationChanged` quando ele chega a um app desktop com identidade; senão (a Task 1 decide),
  um vigia no `wpndatabase.db-wal` dispara a releitura (com intervalo mínimo de 1 s).
- Filtro: o app do Claude (`AppId` começando por `Claude_`) fica fora.
- Ícone: `AppInfo.DisplayInfo.GetLogo(32x32)`, guardado por app.

## 4. Windows quieto

O balão do Windows não aparece enquanto o Vitral cuida das notificações. O caminho é decidido na Task 1, nesta ordem
de preferência, e o escolhido é registrado aqui:
1. Desligar só o balão (banner), app por app, na configuração de notificações do Windows, guardando o valor antigo e
   devolvendo ao sair.
2. Ligar o "não perturbe" do Windows (caminho não documentado), devolvendo ao sair.
3. Esconder o balão pelo componente nos painéis do Windows (exige o aviso de vida da prova da central).

Nenhum funcionando → o balão continua, e o Gustavo é consultado antes de seguir.

## 5. Chegada no notch

- Uma notificação nova (que não estava na lista anterior) vira um aviso do notch: ícone, nome do app, título e texto,
  6 s, na mesma fila dos avisos do Claude e da agenda; a ficha de permissão passa na frente. Clique abre o app.
- Uma rajada (mais de 3 em 2 s) vira um aviso só: "{n} notificações novas".

## 6. Painel

- O sino da lateral direita mostra o número de notificações; o clique abre o painel `Notificacoes` (novo valor de
  `Painel`), que desce do notch com o vidro das ilhas.
- Lista agrupada por app (o grupo mais recente em cima), cada item com título, texto (até 2 linhas) e "há N min".
- Clique no item abre o app (pela AUMID, `shell:AppsFolder\<AUMID>`); × limpa (`RemoveNotification`); "Limpar" por
  grupo e "Limpar tudo" (`ClearNotifications` ou um por um).
- Rodapé: "Abrir a central do Windows".
- Sem identidade ou sem permissão: o painel mostra o motivo e o botão "Abrir a central do Windows"; o sino volta ao
  comportamento de antes.

## 7. Peças

| Peça | Onde |
|---|---|
| `NotificacaoApp`, `RegrasNotificacoes` (agrupar, ordenar, "há quanto tempo", filtro do Claude, novas x antigas, rajada, textos de estado) | Vitral.Core/Notificacoes |
| `PainelNotificacoesViewModel`, avisos de notificação no `EstadoNotch` | Vitral.Ui/Barra |
| `PacoteVitral` (manifesto e registro), `ServicoNotificacoes`, `WindowsQuieto` | Vitral.App/Notificacoes |
| `PainelNotificacoes` (WPF) e o sino da lateral | Vitral.App/Barra |

## 8. Testes

- Automáticos: agrupamento e ordem, "há quanto tempo", filtro do Claude, diferença entre leituras (novas, sumidas),
  rajada, contador, textos de estado, fila do notch com aviso de notificação, painel (limpar um, grupo, tudo).
- Na máquina, com o Gustavo: registro do pacote no Vitral instalado, permissão, notificação nova (o Vitral pode gerar
  uma de teste), Windows quieto, limpar, abrir o app, fechar o Vitral devolve o balão do Windows.
