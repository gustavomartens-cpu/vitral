# Compila uma DLL da fase 0 com as flags da spec §8 e confere que não depende de libc++/libunwind.
param(
    [Parameter(Mandatory)] [string] $Fonte,
    [Parameter(Mandatory)] [string] $Saida,
    [string] $Def
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\tools\env.ps1')

$generated = Join-Path $PSScriptRoot 'generated'
New-Item -ItemType Directory -Force (Split-Path $Saida) | Out-Null

$flags = @(
    '-std=c++23', '-O2', '-shared', '-mguard=cf-nochecks', '-ffp-exception-behavior=maytrap',
    '-DUNICODE', '-D_UNICODE', '-D__USE_MINGW_ANSI_STDIO=0',
    '-D_WIN32_WINNT=0x0A00', '-DWINVER=0x0A00', '-DNTDDI_VERSION=0x0A00000C', '-DWIN32_LEAN_AND_MEAN',
    '-Wno-deprecated-declarations', '-Wno-unneeded-internal-declaration',
    '-static', "-I$generated"
)
$libs = @('-lole32', '-loleaut32', '-lruntimeobject', '-lshlwapi', '-luser32', '-ladvapi32')
$args = $flags + @($Fonte) + $(if ($Def) { @($Def) } else { @() }) + @('-o', $Saida) + $libs

clang++ @args
if ($LASTEXITCODE -ne 0) { throw "clang++ falhou ($LASTEXITCODE)" }

$imports = llvm-objdump -p $Saida | Select-String 'DLL Name:' | ForEach-Object { ($_ -split ':\s*')[1].Trim() }
$proibidas = $imports | Where-Object { $_ -match '^(libc\+\+|libunwind|libwinpthread)' }
if ($proibidas) { throw "A DLL importa runtime não estático: $($proibidas -join ', ')" }
Write-Host "importações OK (sem libc++/libunwind): $($imports -join ', ')" -ForegroundColor Green
