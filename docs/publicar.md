# Publicar uma versão

Para quem mantém o Vitral. Publicar é gerar o instalador e subir a versão como release do GitHub. As instalações
procuram as releases e se atualizam sozinhas ([Atualização](atualizacao.md)).

São dois tokens do GitHub, criados uma vez e guardados em variáveis de ambiente do seu usuário. Nenhum deles entra no
código, no git ou em arquivo do repositório.

| Variável | Para quê | Permissão |
|----------|----------|-----------|
| `VITRAL_TOKEN_ATUALIZACAO` | vai **dentro do app**, para ele ler as releases | Contents: **Read-only** |
| `VITRAL_TOKEN_PUBLICAR` | fica **só no seu PC**, para subir as releases | Contents: **Read and write** |

## 1. Criar os tokens (uma vez)

Faça os passos abaixo duas vezes, uma para cada token.

1. Entre no GitHub com a conta dona do repositório.
2. Clique na sua foto, no canto superior direito, e em **Settings**.
3. No menu da esquerda, lá no fim, clique em **Developer settings**.
4. Clique em **Personal access tokens** → **Fine-grained tokens**.
5. Clique em **Generate new token**. O GitHub pode pedir a senha ou o código de dois fatores.
6. Preencha:
   - **Token name:** `vitral-atualizacao` (ou `vitral-publicar`);
   - **Expiration:** escolha **Custom** e uma data daqui a um ano (anote no calendário: quando vencer, siga a seção 5);
   - **Resource owner:** a sua conta (`gustavomartens-cpu`);
   - **Repository access:** **Only select repositories** → escolha **vitral**.
7. Em **Permissions** → **Repository permissions**, ache **Contents** e escolha:
   - **Read-only** para o `vitral-atualizacao`;
   - **Read and write** para o `vitral-publicar`.

   O GitHub marca **Metadata: Read-only** junto, sozinho. Não marque mais nada.
8. Clique em **Generate token** no fim da página e **copie o token na hora**: o GitHub não mostra de novo.

## 2. Guardar os tokens no seu PC (uma vez)

No terminal do Windows (PowerShell ou Terminal, aberto pelo Menu Iniciar), troque `cole-o-token-aqui` pelo token
copiado, mantendo as aspas, e rode uma linha de cada vez:

```powershell
setx VITRAL_TOKEN_ATUALIZACAO "cole-o-token-aqui"
setx VITRAL_TOKEN_PUBLICAR "cole-o-token-aqui"
```

> Rode com o token de verdade: o `setx` grava exatamente o que estiver entre as aspas. Cole os tokens **só no
> terminal**, nunca em chat, e-mail ou arquivo. Se um token for parar num lugar desses, trate como vazado (seção 5).

**Feche e abra o terminal de novo:** o `setx` só vale para terminais novos. Para conferir sem mostrar o valor:

```powershell
[bool]$env:VITRAL_TOKEN_ATUALIZACAO; [bool]$env:VITRAL_TOKEN_PUBLICAR
```

As duas linhas devem dizer `True`.

## 3. Publicar

1. Suba o número da versão em `src/Vitral.App/Vitral.App.csproj` (`<Version>`), faça o commit e envie para o GitHub
   (`git push`): a tag da release marca esse commit, então ele precisa estar lá.
2. Opcional: escreva as notas da versão em `docs/notas/X.Y.Z.md`. Elas viram o texto da release.
3. Rode, na pasta do repositório:

   ```powershell
   pwsh build.ps1 -Versao X.Y.Z -Publicar
   ```

   O script roda os testes, compila com o token de leitura dentro, baixa a release anterior (para gerar o pacote de
   diferença, que é menor), empacota e sobe tudo como a release `vX.Y.Z`, já publicada.

Se faltar uma das variáveis, ele para logo no começo com "Defina VITRAL_TOKEN_... (veja docs/publicar.md)" e não
publica nada.

## 4. Conferir a release

1. Abra <https://github.com/gustavomartens-cpu/vitral/releases>.
2. A versão nova deve estar no topo, com o nome **Vitral X.Y.Z**, a tag **vX.Y.Z** e a marca **Latest** (não **Draft**
   nem **Pre-release**).
3. Em **Assets** devem estar: `VitralApp-win-Setup.exe`, `VitralApp-win-Portable.zip`, o pacote `-full.nupkg`, o
   `-delta.nupkg` (a partir da segunda versão publicada assim) e o `releases.win.json`.

Num PC com o Vitral instalado, **Procurar agora** na janela deve achar a versão em poucos segundos.

## Se a publicação parar no meio

O `build.ps1` para no passo que falhou e mostra a mensagem do `vpk` (por exemplo, "Falhou: Subir a release
(GitHub)"). Quem já tem o Vitral instalado não é afetado: o app só enxerga releases publicadas, nunca rascunhos.

1. **Parou antes de "Subir a release":** nada foi para o GitHub. Corrija a causa e rode o mesmo comando de novo.
2. **Parou em "Subir a release":** abra <https://github.com/gustavomartens-cpu/vitral/releases> e procure a versão
   `vX.Y.Z`, publicada pela metade ou como **Draft**.
   - Abra a release, clique no ícone de lixeira (**Delete**) e confirme.
   - Se a tag ficou para trás, apague também: aba **Tags** → `vX.Y.Z` → **Delete**.
3. Apague a pasta `artifacts\release` (ela pode ter pedaços da tentativa) e rode de novo
   `pwsh build.ps1 -Versao X.Y.Z -Publicar`, com a mesma versão.
4. Confira a release como na seção 4.

## 5. Trocar um token que venceu ou vazou

1. Gere um token novo (seção 1) e apague o antigo na mesma tela do GitHub (**Delete**).
2. Atualize a variável com `setx` (seção 2) e reabra o terminal.
3. **Token de publicação:** pronto, publique normalmente.
4. **Token de leitura:** publique uma versão nova. As instalações antigas têm o token velho dentro e não conseguem mais
   ler as releases (a janela delas mostra "acesso negado"): em cada uma, rode o Setup novo à mão uma vez.

## Cuidado

- O **token de leitura vai dentro do app**. Quem tiver o instalador consegue extraí-lo e, com ele, ler o código do
  repositório. Por isso ele só tem leitura e só deste repositório.
- O **token de publicação nunca vai para o app**, nem para mensagens, prints ou arquivos. Se vazar, troque na hora
  (seção 5).
- O `build.ps1` nunca imprime nenhum dos dois.
