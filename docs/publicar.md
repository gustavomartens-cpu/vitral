# Publicar uma versão

Para quem mantém o Vitral. Publicar é gerar o instalador e subir a versão como release do GitHub. As instalações
procuram as releases e se atualizam sozinhas ([Atualização](atualizacao.md)).

O repositório é público desde a 0.8.3: o app lê as releases sem token, e a release sobe pelo login do GitHub CLI
(`gh`). Não há token para criar, guardar ou renovar.

## 1. Preparar o PC (uma vez)

1. .NET 10 SDK e o `vpk` na versão do pacote Velopack do app:

   ```powershell
   dotnet tool install -g vpk --version 1.2.161
   ```

2. Entrar no GitHub CLI com a conta dona do repositório (`gh auth login`). Para conferir: `gh auth status`.

## 2. Publicar

1. Suba o número da versão em `src/Vitral.App/Vitral.App.csproj` (`<Version>`), faça o commit e envie para o GitHub
   (`git push`): a tag da release marca esse commit, então ele precisa estar lá.
2. Opcional: escreva as notas da versão em `docs/notas/X.Y.Z.md`. Elas viram o texto da release.
3. Rode, na pasta do repositório:

   ```powershell
   pwsh build.ps1 -Versao X.Y.Z -Publicar
   ```

   O script roda os testes, compila, baixa a release anterior (para gerar o pacote de diferença, que é menor),
   empacota e sobe tudo como a release `vX.Y.Z`, já publicada e marcada como a mais recente.

Ele para logo no começo, sem publicar nada, se o `gh` não estiver logado, se a versão não for dita, se houver mudanças
sem commit ou se o commit ainda não estiver no GitHub.

## 3. Conferir a release

1. Abra <https://github.com/gustavomartens-cpu/vitral/releases>.
2. A versão nova deve estar no topo, com o nome **Vitral X.Y.Z**, a tag **vX.Y.Z** e a marca **Latest** (não **Draft**
   nem **Pre-release**).
3. Em **Assets** devem estar: `VitralApp-win-Setup.exe`, `VitralApp-win-Portable.zip`, o pacote `-full.nupkg`, o
   `-delta.nupkg`, o `RELEASES` e o `releases.win.json`.

Num PC com o Vitral instalado, **Procurar agora** na janela deve achar a versão em poucos segundos.

## Se a publicação parar no meio

1. **Parou antes de "Subir a release":** nada foi para o GitHub. Corrija a causa e rode o mesmo comando de novo.
2. **Parou em "Subir a release":** apague a release e a tag que ficaram pela metade
   (`gh release delete vX.Y.Z --cleanup-tag`), apague a pasta `artifacts\release` e rode de novo.

## Versões antigas

Até a 0.8.2 o repositório era privado e o app levava um token de leitura embutido. Essas versões continuam se
atualizando enquanto esse token não vencer ou for apagado; depois disso, rode o Setup novo à mão uma vez. Os tokens
`VITRAL_TOKEN_ATUALIZACAO` e `VITRAL_TOKEN_PUBLICAR` podem ser apagados no GitHub quando todas as instalações estiverem
na 0.8.3 ou mais nova.
