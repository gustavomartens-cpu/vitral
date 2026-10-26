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

# O repositório é público desde a 0.8.3: o app lê as releases sem token, e a release sobe pelo login do gh.
if ($Publicar) {
    gh auth status *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Entre no GitHub CLI antes de publicar: gh auth login' -ForegroundColor Red
        exit 1
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
    dotnet publish src/Vitral.App -c Release -r win-x64 --self-contained -p:Version=$Versao -o $publish --nologo -v q
}
if ($Publicar) {
    # A última release publicada, para o pack gerar o pacote de diferença mesmo num PC sem histórico.
    Passo 'Baixar a última release (GitHub)' {
        vpk download github --repoUrl $repositorio --outputDir $release
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
        # A tag marca o commit compilado (que precisa estar no GitHub), não a ponta da main. Sobem só os arquivos desta
        # versão e o índice (releases.win.json, RELEASES), que já traz as anteriores.
        $commit = git rev-parse HEAD
        $arquivos = Get-ChildItem $release -File | Where-Object {
            $_.Name -in 'releases.win.json', 'RELEASES', 'VitralApp-win-Setup.exe', 'VitralApp-win-Portable.zip' -or
            $_.Name -like "VitralApp-$Versao-*.nupkg"
        }
        $notas = Join-Path $PSScriptRoot "docs\notas\$Versao.md"
        $texto = if (Test-Path $notas) { @('--notes-file', $notas) } else { @('--notes', '') }
        gh release create "v$Versao" @($arquivos.FullName) --repo gustavomartens-cpu/vitral --title "Vitral $Versao" `
            --target $commit --latest @texto
    }
}

$setup = Get-ChildItem $release -Filter '*Setup.exe' | Select-Object -First 1
Write-Host "`nPronto: $($setup.FullName) ($([math]::Round($setup.Length / 1MB, 1)) MB)" -ForegroundColor Green
if ($Publicar) { Write-Host "Release: $repositorio/releases/tag/v$Versao" -ForegroundColor Green }
