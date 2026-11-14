# Prova: responder o WhatsApp pelo notch

Data: 2026-11-14 · Código descartável: `spike/whatsapp/` (FlaUI.UIA3 5.0.0, com ok do Gustavo) · WhatsApp Desktop
2.2644.101.0 (Store, `5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App`), interface em português · Windows 11 26200.

## Pergunta

O Vitral consegue (1) saber que chegou mensagem no WhatsApp da máquina, com quem mandou e o texto, e (2) mandar
uma resposta digitada no notch para essa conversa, de forma repetível e sem atrapalhar o uso normal do WhatsApp?

## Resposta curta

**Sim, pelas duas pontas, e há um caminho melhor que a automação de interface: o próprio "Responder" da notificação.**

- **Leitura:** a notificação do WhatsApp chega na central com o nome da conversa no 1º texto e a mensagem no 2º. O
  `ServicoNotificacoes` atual já a recebe. Mas o WhatsApp não gera notificação para toda mensagem (veja Riscos).
- **Resposta pela notificação (recomendada):** o ativador COM de notificações do WhatsApp aceita ser chamado por
  outro processo. Chamando `INotificationActivationCallback::Activate` com os argumentos da ação "Enviar" do toast e
  o texto na caixa `quickReplyInputBoxId`, a resposta saiu na conversa certa em 15 ms, **sem abrir nem mostrar a
  janela**. Testado 1 vez (o combinado com o Gustavo).
- **Resposta por automação de interface (reserva):** 14 de 15 envios certos, 1,2 a 1,7 s cada, com o WhatsApp
  minimizado, em segundo plano ou fechado na bandeja, devolvendo a janela e o foco como estavam. Funciona, mas é
  frágil, pisca a janela e depende do nome da conversa.
- **Link `whatsapp://send?phone=`:** não serve. Rouba o foco e precisa do número, que a notificação não traz.

## 1. Leitura

O toast do WhatsApp (XML lido do banco de notificações do Windows, `wpndatabase.db`):

```xml
<toast launch="action=Click&amp;key=msg%3Afalse_<conversa>%40lid_<mensagem>&amp;tag=<conversa>%40lid">
  <visual><binding template="ToastGeneric">
    <text>Eu - Escritório</text>          <!-- nome da conversa, como aparece no WhatsApp -->
    <text>Teste</text>                    <!-- a mensagem (com "Mostrar prévia" ligado) -->
    <group><subgroup><text>✉  3 mensagens não lidas</text></subgroup></group>
    <image placement="appLogoOverride" src="https://media-...cdn.whatsapp.net/..." />  <!-- foto do contato -->
  </binding></visual>
  <actions>
    <input id="quickReplyInputBoxId" type="text" placeHolderContent="Digite uma resposta" />
    <action content="Enviar" arguments="action=Click&amp;key=msg%3Afalse_<conversa>%40lid_<mensagem>"
            activationType="background" hint-inputId="quickReplyInputBoxId" />
    <action content="Silenciar conversa por 8 horas" ... placement="contextMenu" />  <!-- e mais 3 de silenciar -->
  </actions>
</toast>
```

- O remetente é o **nome da conversa**, não um número: o id é `@lid` (identificador interno do WhatsApp), sem
  telefone.
- Vem também um **selo** (`<badge value="1"/>`) a cada mensagem.
- A linha "✉ 3 mensagens não lidas" fica num `group`; o `ServicoNotificacoes` lê só os textos de primeiro nível do
  `ToastGeneric`, mas vale conferir no notch que ela não entra no texto.
- **Grupos: não verificados.** Nenhuma mensagem de grupo virou notificação durante a prova. O esperado (formato
  comum do WhatsApp) é o nome do grupo no título e "Remetente: texto" na mensagem; confirmar antes de desenhar.

## 2. Resposta

### (a) A ação "Responder" da notificação — funciona por fora da API de notificações

A API pública (`UserNotificationListener`) não expõe as ações de um toast, como a prova das notificações já tinha
visto. Mas quem recebe o clique do Windows é o servidor COM declarado no manifesto do WhatsApp
(`ToastActivatorCLSID = bebd6a5e-1b14-4a57-a195-c772d28b6d15`, `WhatsApp.Root.exe ----AppNotificationActivated:`).
Esse servidor:

- é criado sem erro por um processo comum, sem identidade (`CoCreateInstance(..., CLSCTX_LOCAL_SERVER)` = `S_OK`);
- aceita `Activate(aumid, argumentos da ação "Enviar", [{quickReplyInputBoxId, texto}])` e o WhatsApp envia a
  resposta como se a pessoa tivesse digitado na notificação. Resultado: a mensagem apareceu na conversa do número do
  escritório, com o WhatsApp fechado na bandeja, sem janela nenhuma aparecer. 15 ms para o `Activate` voltar.

Para o Vitral usar isso ele precisa dos **argumentos da ação**, que a `UserNotificationListener` não dá. Eles estão no
`Payload` da tabela `Notification` do `wpndatabase.db` (o mesmo banco da prova anterior,
`spike/notificacoes/`), que pode ser lido com o arquivo aberto pelo Windows (cópia com `FileShare.ReadWrite`).

### (b) Automação de interface (UI Automation 3 / FlaUI)

O WhatsApp novo é uma janela WinUI 3 com um WebView2 dentro (é o WhatsApp Web). A UIA3 enxerga a página inteira, com
os nomes acessíveis em português. A UI Automation do .NET (`System.Windows.Automation`) para no WebView2 e não serve.

O que funcionou, passo a passo:

| Passo | Como | Observação |
|---|---|---|
| Achar elementos | `TreeWalker` da árvore crua, com conjunto de `RuntimeId` já vistos | `FindFirst/FindAll` em descendentes entra em laço (o documento aparece dentro dele mesmo); a árvore "de controle" esconde os `div` com id (`side`, `main`, `pane-side`) |
| Buscar a conversa | `ValuePattern.SetValue(nome)` na caixa "Pesquisar" | o React aceita; resultado em ~300 ms |
| Escolher o resultado | nome exato, só nas seções Conversas/Contatos (para antes de "Mensagens") | a busca realça cada palavra num nó separado: o título precisa juntar todos os textos e emojis da célula |
| Abrir a conversa | `PostMessage` de `WM_LBUTTONDOWN/UP` na janela `Chrome_WidgetWin_0` | `Invoke`, `Select` e `DoDefaultAction` **não** abrem (o WhatsApp abre no `mousedown`) |
| Conferir | primeiro texto do painel `main` = nome (sem emojis) | |
| Escrever | `SetFocus` na caixa + `PostMessage(WM_CHAR)` por caractere na `Chrome_WidgetWin_0` | `SetValue` na caixa (editor Lexical) não faz nada |
| Enviar | `InvokePattern` no botão "Enviar" | |
| Confirmar | caixa vazia e texto presente na conversa | |
| Devolver | minimizada → `SW_SHOWNOACTIVATE` antes e `SW_SHOWMINNOACTIVE` depois; fechada → mostra sem ativar e esconde | minimizada sem restaurar **não** funciona (o clique cai em coordenada inválida) |

**Medidas** (conversa consigo mesmo; `spike/whatsapp/resultados.csv`):

| Estado da janela antes | Tentativas | Certas | Tempo (certas) |
|---|---|---|---|
| Minimizada | 10 | 9 | 1.358 a 1.704 ms |
| Em segundo plano (visível, sem foco) | 5 | 5 | 1.208 a 1.504 ms |
| Fechada na bandeja (só abrir a conversa) | 1 | 1 | 1.336 ms |
| **Total de envios** | **15** | **14 (93%)** | média 1.484 ms |

- A falha (nº 3) foi na escrita: nenhum `WM_CHAR` chegou (o foco na caixa não pegou a tempo). Falhou seguro: nada
  foi enviado e não ficou rascunho. Uma nova tentativa de foco resolveria a maioria.
- Foco do usuário devolvido em 14 de 15. Na que falhou, o WhatsApp estava em primeiro plano e terminou sem foco.

**O que quebra:**

- **Conversa não encontrada:** falha limpa em ~4 s ("nenhuma conversa chamada exatamente ...").
- **Nomes repetidos:** a busca por um grupo de condomínio trouxe o mesmo nome em "Conversas" e em "Grupos em
  comum", e duas conversas podem ter o mesmo nome. Pelo nome não há como distinguir; a prova pega a primeira. Em
  produção teria de recusar quando houver mais de uma.
- **Janela fechada (X):** o processo fica na bandeja e funciona, mas a janela aparece por ~1,3 s. Com o WhatsApp
  realmente encerrado, não há o que automatizar.
- **Minimizada:** precisa restaurar sem ativar; a janela **pisca** na tela por ~1,5 s.
- **Idioma:** depende de textos da interface ("Mensagens", "Enviar"; aceita também "Messages", "Send"). Os ids
  `side`, `main`, `pane-side` e as classes de CSS do WhatsApp podem mudar a qualquer atualização.
- **Efeitos colaterais:** abrir a conversa marca as mensagens como lidas e troca a conversa que o usuário tinha
  aberta no WhatsApp. Uma conversa com rascunho é recusada (para não sobrescrever).

### (c) `whatsapp://send?phone=`

Abre a conversa pelo número em ~3 s, mas **restaura e traz o WhatsApp para frente, tomando o foco**, e não envia
nada sozinho (precisaria do passo de escrever e enviar acima). E a notificação não traz o número (só o id `@lid`).
Não serve para o notch.

## Riscos

1. **O WhatsApp não notifica toda mensagem.** As notificações estavam ligadas no app ("Mostrar notificações: Sempre",
   mensagens ativadas, prévia ligada; não mudei nada). Das mensagens mandadas do número do escritório, as duas
   primeiras (11:31) geraram só o selo e a terceira (11:35) gerou o toast. A última notificação do WhatsApp no
   Windows antes de hoje era de 24/04/2026. Sem toast, não há o que ler nem os argumentos para responder. Precisa de
   mais observação antes de prometer "toda mensagem no notch".
2. **Interface interna, sem contrato.** O CLSID do ativador, o formato dos argumentos (`action=Click&key=msg:...`) e o
   id da caixa (`quickReplyInputBoxId`) são do WhatsApp e podem mudar numa atualização. O banco `wpndatabase.db` também
   não é documentado. Mesma classe de risco da automação, mas com muito menos peças.
3. **Prova da resposta pela notificação com n = 1.** Funcionou, mas falta repetir (10 vezes), responder a uma
   notificação já removida da central e a uma mensagem antiga da mesma conversa (o `key` leva o id da mensagem).
4. **Ligação entre a notificação lida e o XML.** O Vitral recebe o `UserNotification.Id` pela API; falta confirmar que
   é o mesmo `Id` da linha no `wpndatabase.db`, para achar o XML certo sem adivinhar.
5. **O balão calado pelo Vitral** (`ShowBanner = 0` para o WhatsApp, marcado `VitralCalou`) não impediu o toast de
   chegar à central. Não parece ser a causa do risco 1, mas não foi isolado.

## Recomendação

**Fazer, pela rota da notificação, e não pela automação de interface.**

1. Próximo passo, ainda em prova (uma tarde): confirmar o `Id` da API = `Id` do banco; repetir a resposta 10 vezes
   (para um contato de teste); testar resposta a notificação já removida; capturar uma notificação de grupo; observar
   por alguns dias quantas mensagens viram toast.
2. Se passar: no notch, uma notificação do WhatsApp que tenha a ação com `hint-inputId` ganha a mesma ficha de
   resposta do Claude (caixa de texto + Enviar). O Vitral lê o XML pelo `Id`, guarda os argumentos da ação "Enviar" e
   chama o ativador COM. Genérico o bastante para valer para outros apps que usam o mesmo padrão de resposta rápida
   (vale verificar quais).
3. **Automação de interface só se a rota da notificação morrer.** Ela funciona (93%), mas pisca a janela, marca como
   lida, troca a conversa aberta, erra com nomes repetidos e quebra com qualquer mudança visual do WhatsApp.

## Como rodar a prova

Com o WhatsApp aberto e logado, no Windows PowerShell com `. spike/tools/env.ps1`:

```
dotnet build spike/whatsapp -c Release
ProvaWhatsApp abrir "<nome exato da conversa>"
ProvaWhatsApp enviar "<nome>" "<texto>"          # envia de verdade
ProvaWhatsApp rodada "<nome>" 10                  # 10 envios "teste Vitral i/10"
ProvaWhatsApp com                                 # só cria o ativador COM, não aciona
ProvaWhatsApp responder-notif "<arguments da ação Enviar>" "<texto>"   # envia de verdade
```

Os arquivos `*.txt` da pasta (árvore despejada, XML da notificação, contato de teste) têm conteúdo de conversas e
ficam fora do git.
