# Cadastro do Vitral no Google e na Microsoft (agenda no notch)

Para o Vitral ler a agenda com o login da pessoa, o Google e a Microsoft exigem que o app esteja cadastrado na conta de
quem o publica. É feito uma vez. O que sai daqui são três valores que vão em `src/Vitral.App/Agenda/Credenciais.cs`:

| Valor | De onde vem |
|---|---|
| `GoogleClientId` | Google Cloud, credencial "App para computador" |
| `GoogleClientSecret` | a mesma credencial ("chave secreta do cliente") |
| `MicrosoftClientId` | Azure, "ID do aplicativo (cliente)" |

Nenhum deles é senha: em app de computador, o Google e a Microsoft tratam o ID e a "chave secreta" como públicos (quem
autoriza o acesso é a pessoa, no navegador). Podem ir para o repositório.

## Google (Google Cloud Console)

1. Entre em <https://console.cloud.google.com> com a conta que vai ser a dona do app.
2. No seletor de projeto (no alto), **Novo projeto** → nome **Vitral** → Criar. Deixe o projeto selecionado.
3. **APIs e serviços → Biblioteca** → procure **Google Calendar API** → **Ativar**.
4. **APIs e serviços → Tela de consentimento OAuth** (em contas novas aparece como **Google Auth Platform**):
   - **Branding / Informações do app:** nome **Vitral**, e-mail de suporte (o seu), e-mail de contato do
     desenvolvedor (o seu). Salvar.
   - **Público (Audience):** tipo de usuário **Externo**. Status de publicação: deixe em **Teste** por enquanto.
     Em **Usuários de teste**, **Adicionar usuários** → o seu e-mail do Google (e o de quem mais for testar; até 100).
   - **Acesso a dados (Data access) → Adicionar ou remover escopos:** marque
     `https://www.googleapis.com/auth/calendar.readonly` ("Ver os eventos de todas as suas agendas"). Salvar.
5. **Clientes (Clients) → Criar cliente** (ou **Credenciais → Criar credenciais → ID do cliente OAuth**):
   - Tipo de aplicativo: **App para computador** (Desktop app). Nome: **Vitral**. Criar.
   - Copie o **ID do cliente** e a **Chave secreta do cliente**.

Observações:

- Em **Teste**, o acesso concedido vence a cada 7 dias e o Vitral pede para entrar de novo. Para não vencer, mude o
  status para **Em produção** (botão "Publicar app"); sem a verificação do Google, quem entra vê o aviso "o Google não
  verificou este app" (clique em **Avançado → Acessar Vitral**) e o limite é de 100 pessoas.
- Conta do Google Workspace do escritório: o administrador pode bloquear apps não verificados. Se o login disser que o
  app está bloqueado, o administrador libera em **Admin → Segurança → Controles de API → Acesso de apps de terceiros**.

## Microsoft (portal do Azure)

1. Entre em <https://portal.azure.com> (ou <https://entra.microsoft.com>) com a sua conta Microsoft.
2. **Microsoft Entra ID → Registros de aplicativo → Novo registro**:
   - Nome: **Vitral**.
   - Tipos de conta: **Contas em qualquer diretório organizacional e contas Microsoft pessoais**.
   - URI de redirecionamento: plataforma **Cliente público/nativo (móvel e área de trabalho)**, valor
     `http://localhost`.
   - Registrar.
3. Na página do app, copie o **ID do aplicativo (cliente)**.
4. **Autenticação**: em **Configurações avançadas**, **Permitir fluxos de clientes públicos** = **Sim**. Salvar.
5. **Permissões de API → Adicionar uma permissão → Microsoft Graph → Permissões delegadas** → marque
   **Calendars.Read** (e confira que **User.Read** já está lá) → Adicionar.

Observações:

- Contas pessoais (outlook.com, hotmail) entram direto.
- Conta do Microsoft 365 do escritório: se a organização não deixa as pessoas aprovarem apps, o login mostra "precisa da
  aprovação do administrador"; o administrador concede em **Entra ID → Aplicativos empresariais → Vitral → Permissões →
  Conceder consentimento do administrador**.

## Depois

Mande os três valores; eles entram em `Credenciais.cs`. Sem um deles, o botão daquele provedor fica desligado na janela
de contas ("falta cadastrar o app").
