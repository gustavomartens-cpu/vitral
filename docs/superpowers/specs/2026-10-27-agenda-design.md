# Compromissos da agenda no notch (Google e Microsoft)

Data: 27/10/2026. O Vitral passa a ler a agenda do Google e do Outlook/Microsoft 365 (login com a conta, só leitura),
mostra os compromissos no painel do calendário do notch, marca os dias com compromisso na grade, avisa alguns minutos
antes pelo notch e oferece "Entrar" quando há link de reunião.

## 1. Objetivo e escopo

Hoje o painel do calendário mostra só a grade do mês, e o notch avisa só coisas do Claude. O Gustavo quer ver os
compromissos sem abrir o navegador e ser avisado na hora certa, com as duas agendas que usa juntas numa lista.

**Sucesso:** ligar a conta Google e a Microsoft uma vez cada; abrir o calendário e ver os compromissos do dia (e de
qualquer dia clicado); ser avisado 10 min antes (ou 5/15) e entrar na reunião com um clique.

**Fora do escopo:** criar, editar ou responder a convites; próximo compromisso ao lado do relógio no repouso; outros
provedores (iCloud, iCal por link); verificação do app no Google para uso amplo (fica para quando houver mais de 100
pessoas).

## 2. Escolhas do Gustavo

- As duas agendas, numa lista só (C).
- Lista no painel (A), aviso antes (C), entrar na reunião (D), pontinho nos dias da grade (E). Sem o próximo compromisso
  ao lado do relógio (B).
- Login com a conta (OAuth) nos dois provedores (abordagem 1).
- Contas e tokens são da máquina, não do perfil.

## 3. Modelo (Vitral.Core/Agenda)

```csharp
public enum ProvedorAgenda { Google, Microsoft }

public sealed record ContaAgenda(ProvedorAgenda Provedor, string Email, string Cor);

public sealed record Compromisso(string Id, ContaAgenda Conta, string Titulo, DateTimeOffset Inicio, DateTimeOffset Fim,
    bool DiaInteiro, string? Local, Uri? LinkReuniao, Uri? LinkEvento, bool Recusado, bool Cancelado);

public interface IFonteAgenda
{
    ContaAgenda Conta { get; }
    Task<IReadOnlyList<Compromisso>> Ler(DateTimeOffset de, DateTimeOffset ate, CancellationToken cancelar);
}
```

- `ConfigApp.Agenda`: `AgendaConfig(IReadOnlyList<ContaAgenda> Contas, int AntecedenciaMin = 10, bool Avisar = true)`.
  Nunca contém token.
- Cor da conta: a primeira conta Google azul `#4C7DF0`, a primeira Microsoft `#2BA9E0`; as seguintes, de uma paleta fixa
  de seis cores, na ordem em que foram ligadas.

## 4. Regras (Vitral.Core/Agenda, puras)

### 4.1 Junção

`ServicoAgenda` lê todas as fontes do intervalo, junta, descarta `Cancelado` e `Recusado`, ordena por (`DiaInteiro`
primeiro, `Inicio`, `Titulo`). Uma fonte que falha não derruba as outras: fica a última leitura boa daquela conta, e
`AtualizadoEm` da conta não avança.

### 4.2 Link da reunião

`LinkReuniao` vem do campo próprio do provedor (Google: `hangoutLink` ou `conferenceData.entryPoints[type=video]`;
Microsoft: `onlineMeeting.joinUrl`). Sem ele, `ExtrairLink(local, descricao)` procura, nesta ordem, o primeiro endereço
`https://meet.google.com/…`, `https://teams.microsoft.com/l/meetup-join/…`, `https://*.zoom.us/j/…`.

### 4.3 Aviso

`ProximosAvisos(compromissos, agora, antecedencia, jaAvisados)` devolve os compromissos não de dia inteiro com
`Inicio − antecedência ≤ agora < Fim` que ainda não foram avisados. A chave de "já avisado" é `(Id, Inicio)`: se o
horário mudar, avisa de novo. Depois de suspensão: um compromisso cujo aviso passou e que ainda não terminou é avisado
uma vez; um que já terminou, não.

### 4.4 Dias marcados

`DiasComCompromisso(compromissos, mes, fuso)` devolve os dias do mês que têm compromisso no fuso do Windows (um
compromisso de vários dias marca todos).

### 4.5 Fuso

Tudo é comparado em `DateTimeOffset`; mostrado e agrupado por dia no fuso local do Windows. Dia inteiro do Google
(`date`) e da Microsoft (`isAllDay`) vira meia-noite local daquele dia até a meia-noite do dia seguinte.

## 5. No Vitral (App)

### 5.1 Login e tokens

- **Google:** `Google.Apis.Calendar.v3` com `GoogleWebAuthorizationBroker` (navegador do sistema, retorno pelo endereço
  local), escopo `https://www.googleapis.com/auth/calendar.readonly`. Armazenamento de token próprio (`IDataStore`) com
  DPAPI.
- **Microsoft:** `Microsoft.Identity.Client` (MSAL), cliente público, autoridade `common`, navegador do sistema,
  escopo `Calendars.Read`; cache de token com `Microsoft.Identity.Client.Extensions.Msal` (DPAPI). Leitura por
  `GET https://graph.microsoft.com/v1.0/me/calendarView?startDateTime=…&endDateTime=…` com `HttpClient`, paginando por
  `@odata.nextLink`, cabeçalho `Prefer: outlook.timezone="UTC"`.
- Tokens em `%LOCALAPPDATA%\Vitral\contas\` (um arquivo por conta), cifrados com DPAPI do usuário. Nunca em perfil,
  exportação, log ou config.
- Remover a conta apaga o arquivo e revoga no provedor (Google: `oauth2.googleapis.com/revoke`; Microsoft: remove a
  conta do cache do MSAL).
- Client ids no código (`Agenda/Credenciais.cs`); não são segredo em app de computador. Guia de cadastro em
  `docs/agenda-cadastro.md`.

### 5.2 Leitura

`ServicoAgendaApp` (App): lê de 1º do mês anterior ao fim do mês seguinte (para a grade e a lista); a cada 5 min, ao
voltar da suspensão e ao abrir o painel; confere os avisos a cada 30 s.

### 5.3 Janela de contas

Na barra lateral, abaixo de "Avisos do Claude no notch": caixa "Agenda no notch" (liga/desliga os avisos) e o link
"Contas…". A janela "Contas da agenda" lista as contas (cor, e-mail, provedor, estado: "ok", "atualizado às 14:05" ou
"Precisa entrar de novo" com botão Entrar), cada uma com Remover; botões "Entrar com Google" e "Entrar com Microsoft";
"Avisar antes": 5 / 10 / 15 min.

### 5.4 Painel do calendário

- A grade marca os dias com compromisso (pontinho embaixo do número).
- Embaixo, a lista do dia (hoje ao abrir; o dia clicado na grade): dia inteiro primeiro; depois horário (`14:30–15:30`),
  barrinha na cor da conta, título; pílula "Entrar" se houver `LinkReuniao`. Clique no compromisso abre `LinkEvento`.
- Sem contas: convite "Ligue sua agenda em Contas…". Com dados antigos: "atualizado às 14:05".

### 5.5 Aviso no notch

- `EstadoNotch` ganha a fila de avisos da agenda (`AvisoAgenda(Compromisso, Antecedencia)`). Prioridade: ficha de
  permissão > ficha de resposta > aviso do Claude e aviso da agenda (na ordem em que chegaram).
- Aviso: ícone de calendário, "Título · em 10 min" (ou "· agora"), detalhe "14:30–15:30 · Google Meet" (ou o local), pílula
  "Entrar" com link. Depois de 6 s vira lembrete, que fica até o clique ou até `Fim`.
- Clique no lembrete (ou no aviso fora do Entrar): abre o painel do calendário no dia do compromisso. Entrar: abre o link.
- Tela cheia: o aviso espera, como os outros.

## 6. Erros e bordas

- Token vencido ou revogado: conta em "Precisa entrar de novo"; dados antigos continuam; nada no notch.
- Sem internet / provedor fora: última leitura boa; tenta no próximo ciclo; sem aviso de erro.
- Conta de empresa que exige aprovação do administrador: o login mostra a mensagem do provedor; a conta não é
  adicionada; a janela diz "a organização precisa aprovar o Vitral".
- Evento alterado: aviso refeito pela chave `(Id, Inicio)`. Evento cancelado: aviso não sai; lembrete pendente some.
- Vários compromissos no mesmo horário: um aviso por compromisso, em fila.
- Mesma conta ligada duas vezes: a segunda substitui a primeira.

## 7. Testes

- **Core:** junção e ordem; cancelados e recusados fora; fonte com falha mantém a anterior; link pelo campo próprio e
  pela descrição (Meet, Teams, Zoom); próximo aviso (antecedência, não repetir, horário mudou, suspensão, dia inteiro
  fora); dias marcados (vários dias, fuso); dia inteiro → meia-noite local; config sem token.
- **App:** conversão de respostas reais gravadas (JSON do Google `events.list` e do Graph `calendarView`) em
  `Compromisso`.
- **Ui:** `EstadoNotch` com aviso da agenda (fila, prioridade da permissão, lembrete até o fim, clique abre o calendário);
  lista do painel por dia.
- **Na tela, com o Gustavo:** ligar as duas contas, lista, pontinhos, aviso, Entrar, remover conta, token revogado.

## 8. Riscos

- **App não verificado no Google:** aviso "o Google não verificou este app" no login e limite de 100 usuários. Para uso
  amplo, verificação (política de privacidade, domínio, vídeo) num projeto à parte.
- **Contas do escritório:** o administrador do Workspace ou do Microsoft 365 pode bloquear apps de terceiros.
- **Pacotes novos:** `Google.Apis.Calendar.v3`, `Microsoft.Identity.Client`, `Microsoft.Identity.Client.Extensions.Msal`
  aumentam o instalador.
