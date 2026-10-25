# Testa, publica e empacota o Vitral num Setup.exe por usuário (Velopack).
# Uso: pwsh build.ps1 [-Versao 0.1.0]
param([string] $Versao = '0.1.0')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
. (Join-Path $PSScriptRoot 'tools\env.ps1')

function Passo([string] $nome, [scriptblock] $acao) {
    Write-Host "`n== $nome" -ForegroundColor Cyan
    & $acao
    if ($LASTEXITCODE -ne 0) { throw "Falhou: $nome (código $LASTEXITCODE)" }
}

$publish = Join-Path $PSScriptRoot 'artifacts\publish'
$release = Join-Path $PSScriptRoot 'artifacts\release'

Passo 'Testes' { dotnet test --nologo -v q }
Passo 'Publicar (win-x64, com o .NET embutido)' {
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    dotnet publish src/Vitral.App -c Release -r win-x64 --self-contained -p:Version=$Versao -o $publish --nologo -v q
}
Passo 'Empacotar (Velopack)' {
    # packId diferente da pasta de dados (%LOCALAPPDATA%\Vitral): o Velopack instala em %LOCALAPPDATA%\<packId>
    # e apaga essa pasta ao desinstalar. Com o mesmo nome, desinstalar apagaria os perfis.
    vpk pack --packId VitralApp --packVersion $Versao --packDir $publish --mainExe Vitral.exe --runtime win-x64 `
        --packTitle Vitral --packAuthors 'Gustavo Martens' --icon src/Vitral.App/Recursos/vitral.ico --outputDir $release
}

$setup = Get-ChildItem $release -Filter '*Setup.exe' | Select-Object -First 1
Write-Host "`nPronto: $($setup.FullName) ($([math]::Round($setup.Length / 1MB, 1)) MB)" -ForegroundColor Green
