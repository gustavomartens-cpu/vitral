# Compila o componente do Iniciar (src/Vitral.Iniciar) para artifacts/iniciar/VitralIniciar.dll (spec do Iniciar §4.1).
# Gera a projeção C++/WinRT na primeira vez; usa as flags e a checagem de importações da fase 0 (spike/tap/build.ps1).
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot
. (Join-Path $PSScriptRoot 'env.ps1')

$fonte = Join-Path $raiz 'src\Vitral.Iniciar'
$projecao = Join-Path $fonte 'generated'
if (-not (Test-Path (Join-Path $projecao 'winrt\Windows.UI.Xaml.h'))) {
    cppwinrt -input local -output $projecao
    if ($LASTEXITCODE -ne 0) { throw "cppwinrt falhou ($LASTEXITCODE)" }
}

$saida = Join-Path $raiz 'artifacts\iniciar\VitralIniciar.dll'
New-Item -ItemType Directory -Force (Split-Path $saida) | Out-Null
$flags = @(
    '-std=c++23', '-O2', '-shared', '-mguard=cf-nochecks', '-ffp-exception-behavior=maytrap',
    '-DUNICODE', '-D_UNICODE', '-D__USE_MINGW_ANSI_STDIO=0',
    '-D_WIN32_WINNT=0x0A00', '-DWINVER=0x0A00', '-DNTDDI_VERSION=0x0A00000C', '-DWIN32_LEAN_AND_MEAN',
    '-Wno-deprecated-declarations', '-Wno-unneeded-internal-declaration',
    '-static', "-I$projecao"
)
$libs = @('-lole32', '-loleaut32', '-lruntimeobject', '-lshlwapi', '-luser32', '-ladvapi32')
clang++ @flags (Join-Path $fonte 'VitralIniciar.cpp') (Join-Path $fonte 'VitralIniciar.def') -o $saida @libs
if ($LASTEXITCODE -ne 0) { throw "clang++ falhou ($LASTEXITCODE)" }

$imports = llvm-objdump -p $saida | Select-String 'DLL Name:' | ForEach-Object { ($_ -split ':\s*')[1].Trim() }
$proibidas = $imports | Where-Object { $_ -match '^(libc\+\+|libunwind|libwinpthread)' }
if ($proibidas) { throw "A DLL importa runtime não estático: $($proibidas -join ', ')" }
Write-Host "importações OK: $saida"
