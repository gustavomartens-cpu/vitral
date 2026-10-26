# Atualização automática pelo GitHub

Data: 26/10/2026. O Vitral passa a se atualizar sozinho a partir das releases do repositório no GitHub. O repositório
ganha um passo a passo de instalação, atualização e publicação, escrito para quem nunca viu o projeto.

## 1. Objetivo

O Gustavo usa o Vitral em dois computadores, e o segundo fica para trás a cada versão. Ele quer instalar à mão uma
última vez e, dali em diante, receber as versões novas sozinho. Outras pessoas podem vir a usar o Vitral, então a
instalação, a atualização e a publicação precisam estar descritas no repositório.

## 2. Escolhas do Gustavo

| Item | Escolha |
|------|---------|
| Fonte | releases do repositório `gustavomartens-cpu/vitral`, que é privado, com um **token de leitura embutido no app** |
| Comportamento | baixa sozinho em segundo plano e avisa no notch ("Vitral X pronto"); o clique reinicia; sem clique, a versão nova entra na próxima vez que o Vitral abrir |
| Mecanismo | o atualizador do próprio Velopack (`UpdateManager` com `GithubSource`) e a publicação com `vpk upload github` |

**Consequência aceita:** quem tiver o instalador consegue extrair o token de leitura e, com ele, ler o código do
repositório.

## 3. Tokens

Os dois tokens são criados pelo Gustavo no GitHub e guardados em variáveis de ambiente do usuário. Nenhum dos dois
aparece no código, no git ou em arquivos do repositório.

| Variável | Para quê | Permissões (token *fine-grained*, só o repositório `vitral`) |
|----------|----------|------------------------------------------------------------|
| `VITRAL_TOKEN_ATUALIZACAO` | vai dentro do app; o app só lê releases | Contents: Read-only |
| `VITRAL_TOKEN_PUBLICAR` | só no PC de quem publica; nunca vai para o app | Contents: Read and write |

**Como o token de leitura entra no app:**

- o `build.ps1` passa a variável ao `dotnet publish` como `-p:TokenAtualizacao=...`;
- o `Vitral.App.csproj` a transforma em `AssemblyMetadata("TokenAtualizacao", ...)`, só quando a propriedade não está
  vazia;
- o app lê o valor com `TokenEmbutido.Ler(Assembly) → string?`.

Sem a variável (builds de desenvolvimento, e o `dotnet build` comum), não há token, e a atualização fica desligada.

## 4. Publicação

O `build.ps1` ganha o parâmetro `-Publicar`. Ele exige as duas variáveis e para com uma mensagem clara se faltar
alguma.

**Antes de empacotar,** baixa a última release com `vpk download github --repoUrl <repo> --token <publicar> --outputDir artifacts\release`.
Assim o pacote de diferença sai mesmo num PC sem histórico.

**Depois do `vpk pack`, roda:**

```
vpk upload github --repoUrl https://github.com/gustavomartens-cpu/vitral --token <publicar>
                  --tag v<versao> --releaseName "Vitral <versao>" --publish --merge
```

O comando sobe o Setup, o Portable, o pacote completo, o pacote de diferença e o `releases.win.json`.

**Notas da release:** vêm de `docs/notas/<versao>.md` se o arquivo existir, pelo parâmetro de notas do `vpk upload`.
Senão, ficam vazias e podem ser editadas no GitHub depois.

Sem `-Publicar`, o `build.ps1` continua como hoje, só local.

## 5. O verificador (no App)

### 5.1 Regra (Core, pura)

`ServicoAtualizacao(IFonteAtualizacao fonte, Func<DateTime> agora)` em `src/Vitral.Core/Atualizacao/`:

**Fonte:**

- `IFonteAtualizacao`:
  - `bool Ligada { get; }`
  - `Task<string?> ProcurarAsync()`: a versão nova, ou null se não houver;
  - `Task BaixarAsync()`;
  - `void Reiniciar()`.

  As falhas chegam como exceção.

**Estado:**

- `enum EstadoAtualizacao { Desligada, Atualizado, Baixando, Pronta, Falhou, AcessoNegado }`, mais a `string? Versao`;
- `event Action? Mudou` e `event Action<string>? Pronta`, que dispara uma vez por versão.

**Tempo:**

- `Task TiqueAsync()` é chamado pelo App a cada minuto;
- confere 1 min depois de criado e depois a cada 6 h;
- `Task ProcurarAgoraAsync()` confere na hora (é o botão).

**Fluxo:**

- **versão nova:** `Baixando` → `BaixarAsync` → `Pronta` e o evento `Pronta(versao)`;
- **mesma versão:** `Atualizado`;
- **falha:** `HttpRequestException` com 401 ou 403 → `AcessoNegado`; outra falha → `Falhou`. Nenhum aviso no notch, e
  tenta de novo na próxima conferência;
- **fonte desligada:** `Desligada` e nunca confere;
- **já em `Pronta`:** não confere de novo até reiniciar.

### 5.2 Fonte real (App)

`FonteVelopack : IFonteAtualizacao` usa
`new UpdateManager(new GithubSource("https://github.com/gustavomartens-cpu/vitral", token, prerelease: false))`.

- **`Ligada`:** existe token e o `UpdateManager` está instalado (`IsInstalled`).
- **`ProcurarAsync`:** `CheckForUpdatesAsync`; com resultado, guarda o `UpdateInfo` e devolve a versão.
- **`BaixarAsync`:** `DownloadUpdatesAsync(info)`.
- **`Reiniciar`:** faz a limpeza de saída do App (o mesmo `Sair` de sempre, sem o `Shutdown` final) e depois
  `WaitExitThenApplyUpdates(info.TargetFullRelease, silent: true, restart: true)` e `Shutdown`.

Sem clique, o Velopack aplica a versão baixada quando o Vitral abre da próxima vez (`VelopackApp.Run`).

**Pré-releases** (como a 0.6.1) são ignoradas.

### 5.3 Aviso no notch

`TipoAviso` ganha `Atualizacao`. O aviso é `new AvisoClaude(TipoAviso.Atualizacao, "Vitral", "<versão> pronto")`.

**O que muda no notch:**

- **símbolo:** o logo do Vitral (`Logo`), no lugar do símbolo do Claude;
- **título:** "Vitral <versão> pronto";
- **linha de baixo:** "clique para reiniciar e atualizar";
- **clique no aviso:** `AcaoNotch.Reiniciar` (novo), que chama `Reiniciar`;
- **lembrete:** igual ao do Claude, e a lateral esquerda mostra "Atualização pronta";
- **descarte:** o aviso de atualização não descarta pedidos de permissão. A sessão "Vitral" nunca é a de um pedido.

### 5.4 Na janela principal

Embaixo do texto da versão, na barra lateral, entram uma linha de estado e o link "Procurar agora".

| Estado | Linha |
|--------|-------|
| Desligada | "Atualização desligada (versão de desenvolvimento)" |
| Atualizado | "Atualizado" |
| Baixando | "Baixando a versão <v>…" |
| Pronta | "Versão <v> pronta — **reiniciar**", com o "reiniciar" clicável |
| Falhou | "Não foi possível procurar atualizações" |
| AcessoNegado | "Não foi possível procurar atualizações (acesso negado: o token venceu?)" |

A versão atual aparece como "Vitral <versão>", lida do `UpdateManager.CurrentVersion` ou do assembly.

## 6. Documentação

| Arquivo | Para quem | Conteúdo |
|---------|-----------|----------|
| `README.md` (novo, raiz) | todos | o que é o Vitral (duas frases, um print), requisitos (Windows 11), links para os três guias |
| `docs/instalar.md` (o atual `instalar-no-notebook.md`, renomeado) | quem usa | baixar o Setup da release mais nova (exige a conta do GitHub logada: repositório privado), SmartScreen e Controle Inteligente de Aplicativos, perfis, desinstalação |
| `docs/atualizacao.md` (novo) | quem usa | como a atualização chega (aviso no notch, reinício), como ver a versão e procurar agora, o que fazer se não atualizar (Setup novo por cima), e que pré-releases não chegam por atualização |
| `docs/publicar.md` (novo) | quem mantém | ver abaixo |

**Passos do `docs/publicar.md`:**

1. Criar os dois tokens, com o caminho exato no GitHub: Settings → Developer settings → Personal access tokens →
   Fine-grained tokens → Generate new token. Para cada um: nome, validade, repositório e permissão.
2. Guardar cada um com `setx VITRAL_TOKEN_ATUALIZACAO "..."` e `setx VITRAL_TOKEN_PUBLICAR "..."`, e reabrir o
   terminal.
3. Gerar e publicar: `pwsh build.ps1 -Versao X.Y.Z -Publicar`.
4. Conferir a release no GitHub.
5. Trocar um token que venceu ou vazou: gerar outro, atualizar a variável e publicar uma versão nova. As instalações
   antigas, com o token de leitura vencido, precisam do Setup novo uma vez.
6. Seção "Cuidado": o token de leitura vai dentro do app e pode ser extraído; o de publicação nunca.

## 7. Testes

**Com teste escrito antes:**

- `ServicoAtualizacao`, com fonte e relógio falsos:
  - não confere antes de 1 min, confere em 1 min e de novo em 6 h;
  - versão nova vai a `Baixando`, depois a `Pronta`, e dispara `Pronta` uma vez;
  - mesma versão fica em `Atualizado`;
  - falha comum vai a `Falhou` sem `Pronta`, e confere de novo no próximo ciclo;
  - 401 ou 403 vai a `AcessoNegado`;
  - fonte desligada fica em `Desligada` e nunca chama a fonte;
  - `ProcurarAgoraAsync` confere fora do ciclo;
  - em `Pronta`, não confere de novo.
- `TokenEmbutido.Ler`: um assembly com e sem o metadado; o vazio vira null.
- `EstadoNotch`: aviso de atualização, clique devolvendo `Reiniciar`, lembrete com "Atualização pronta", e um pedido
  de permissão aberto que não é descartado pelo aviso de atualização.
- O texto da linha de estado para cada `EstadoAtualizacao`, numa função pura `TextoAtualizacao.Para(estado, versao)`.

**Na prática, com o Gustavo:**

1. Publicar a 0.7.2, com a atualização.
2. O Gustavo instala à mão no notebook.
3. Publicar a 0.7.3 (sem mudanças).
4. Conferir que os dois recebem o aviso no notch e se atualizam pelo clique, e que, sem clique, a versão nova entra ao
   reabrir o Vitral.

## 8. Riscos

- **Token vencido ou revogado:** a atualização para em silêncio. A linha de estado mostra o acesso negado, e o
  `docs/publicar.md` explica a troca.
- **Reinício no meio de um pedido de permissão:** só acontece pelo clique do Gustavo; a limpeza de saída solta os
  pedidos, que seguem no Claude.
- **O release do upload sai como rascunho, ou com tag errada:** o `--publish` e a `--tag` explícita evitam. A
  conferência da release faz parte do passo a passo.
- **Pacote de diferença sem a versão anterior:** o `vpk download github` antes do `pack` resolve. Sem ele, só o pacote
  completo, que também funciona, mas maior.

## 9. Fora desta fase

- Assinatura digital do instalador, que tiraria o aviso do SmartScreen. É paga.
- Repositório público só de releases (a alternativa sem token).
- Canal de pré-releases para testar antes.
