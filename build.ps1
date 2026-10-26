# Testa, publica e empacota o Vitral num Setup.exe por usuário (Velopack).
# Uso: pwsh build.ps1 [-Versao 0.1.0] [-Publicar]
# -Publicar sobe a versão como release do GitHub, de onde as instalações se atualizam (docs/publicar.md).
param([string] $Versao = '0.1.0', [switch] $Publicar)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
. (Join-Path $PSScriptRoot 'tools\env.ps1')

$repositorio = 'https://github.com/gustavomartens-cpu/vitral'

function Passo([string] $nome, [scriptblock] $acao) {
    Write-Host "`n== $nome" -ForegroundColor Cyan
    & $acao
    if ($LASTEXITCODE -ne 0) { throw "Falhou: $nome (código $LASTEXITCODE)" }
}

# Publicar exige os dois tokens (nenhum é impresso): o de leitura vai dentro do app, o de publicação só sobe a release.
if ($Publicar) {
    foreach ($nome in 'VITRAL_TOKEN_ATUALIZACAO', 'VITRAL_TOKEN_PUBLICAR') {
        if (-not [Environment]::GetEnvironmentVariable($nome)) {
            Write-Host "Defina $nome (veja docs/publicar.md)" -ForegroundColor Red
            exit 1
        }
    }
    # A release marca o commit compilado: a versão tem de ser dita, sem mudanças fora do commit, e o commit no GitHub.
    $problema =
        if (-not $PSBoundParameters.ContainsKey('Versao')) { 'Diga a versão: pwsh build.ps1 -Versao X.Y.Z -Publicar' }
        elseif (git status --porcelain) { 'Há mudanças sem commit: faça o commit antes de publicar' }
        elseif (-not (git branch -r --contains HEAD)) { 'O commit atual não está no GitHub: rode git push antes de publicar' }
    if ($problema) {
        Write-Host $problema -ForegroundColor Red
        exit 1
    }
}

$publish = Join-Path $PSScriptRoot 'artifacts\publish'
$release = Join-Path $PSScriptRoot 'artifacts\release'

Passo 'Testes' { dotnet test --nologo -v q }
Passo 'Publicar (win-x64, com o .NET embutido)' {
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    # Token de leitura das releases (docs/publicar.md): embutido no app para a atualização automática. Nunca é impresso.
    $token = @()
    if ($env:VITRAL_TOKEN_ATUALIZACAO) { $token = @("-p:TokenAtualizacao=$env:VITRAL_TOKEN_ATUALIZACAO") }
    else { Write-Host 'sem VITRAL_TOKEN_ATUALIZACAO: esta versão não se atualiza sozinha' -ForegroundColor Yellow }
    dotnet publish src/Vitral.App -c Release -r win-x64 --self-contained -p:Version=$Versao @token -o $publish --nologo -v q
}
if ($Publicar) {
    # A última release publicada, para o pack gerar o pacote de diferença mesmo num PC sem histórico.
    Passo 'Baixar a última release (GitHub)' {
        vpk download github --repoUrl $repositorio --token $env:VITRAL_TOKEN_PUBLICAR --outputDir $release
    }
}
Passo 'Empacotar (Velopack)' {
    # packId diferente da pasta de dados (%LOCALAPPDATA%\Vitral): o Velopack instala em %LOCALAPPDATA%\<packId>
    # e apaga essa pasta ao desinstalar. Com o mesmo nome, desinstalar apagaria os perfis.
    # Notas da versão (docs/notas/<versão>.md), se existirem: viram o texto da release no GitHub.
    $notas = @()
    $arquivoNotas = Join-Path $PSScriptRoot "docs\notas\$Versao.md"
    if (Test-Path $arquivoNotas) { $notas = @('--releaseNotes', $arquivoNotas) }
    vpk pack --packId VitralApp --packVersion $Versao --packDir $publish --mainExe Vitral.exe --runtime win-x64 `
        --packTitle Vitral --packAuthors 'Gustavo Martens' --icon src/Vitral.App/Recursos/vitral.ico --outputDir $release @notas
}
if ($Publicar) {
    Passo 'Subir a release (GitHub)' {
        # A tag marca o commit compilado (que precisa estar no GitHub), não a ponta da main.
        $commit = git rev-parse HEAD
        vpk upload github --repoUrl $repositorio --token $env:VITRAL_TOKEN_PUBLICAR --outputDir $release `
            --tag "v$Versao" --releaseName "Vitral $Versao" --targetCommitish $commit --publish --merge
    }
}

$setup = Get-ChildItem $release -Filter '*Setup.exe' | Select-Object -First 1
Write-Host "`nPronto: $($setup.FullName) ($([math]::Round($setup.Length / 1MB, 1)) MB)" -ForegroundColor Green
if ($Publicar) { Write-Host "Release: $repositorio/releases/tag/v$Versao" -ForegroundColor Green }
