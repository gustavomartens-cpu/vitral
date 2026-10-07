# Gera os headers C++/WinRT a partir dos .winmd do próprio Windows (C:\Windows\System32\WinMetadata).
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\tools\env.ps1')
$saida = Join-Path $PSScriptRoot 'generated'
if (Test-Path (Join-Path $saida 'winrt\Windows.UI.Xaml.h')) { Write-Host "Projeção já existe em $saida"; return }
cppwinrt -input local -output $saida
if ($LASTEXITCODE -ne 0) { throw "cppwinrt falhou ($LASTEXITCODE)" }
Write-Host "Projeção gerada em $saida"
