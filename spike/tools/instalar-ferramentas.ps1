# Instala as ferramentas da fase 0 em G:\dev (sem admin, sem mexer no PATH do sistema).
# Idempotente: pula o que já existe. Pede confirmação antes de cada download.
$ErrorActionPreference = 'Stop'

$Raiz       = 'G:\dev'
$DotnetDir  = Join-Path $Raiz 'dotnet'
$Toolchains = Join-Path $Raiz 'toolchains'
$LlvmNome   = 'llvm-mingw-20260922-ucrt-x86_64'
$LlvmDir    = Join-Path $Toolchains $LlvmNome
$WinrtDir   = Join-Path $Toolchains 'cppwinrt'
$Tmp        = Join-Path $Raiz '_downloads'

$Downloads = @{
    DotnetInstall = @{ Url = 'https://dot.net/v1/dotnet-install.ps1'; Tamanho = '75 KB (o script baixa depois o SDK do .NET 10, ~290 MB, de builds.dotnet.microsoft.com)' }
    Llvm          = @{ Url = "https://github.com/mstorsjo/llvm-mingw/releases/download/20260922/$LlvmNome.zip"; Tamanho = '182 MB (≈ 600 MB extraído)' }
    CppWinRT      = @{ Url = 'https://api.nuget.org/v3-flatcontainer/microsoft.windows.cppwinrt/2.0.250303.1/microsoft.windows.cppwinrt.2.0.250303.1.nupkg'; Tamanho = '0,6 MB' }
}

function Confirmar([string]$nome, $d) {
    Write-Host ""
    Write-Host "Download: $nome" -ForegroundColor Cyan
    Write-Host "  Origem : $($d.Url)"
    Write-Host "  Tamanho: $($d.Tamanho)"
    $r = Read-Host '  Baixar? (s/n)'
    if ($r -notin @('s', 'S', 'sim')) { throw "Cancelado pelo usuário: $nome" }
}

New-Item -ItemType Directory -Force $Toolchains, $Tmp | Out-Null
$livreAntes = (Get-PSDrive C).Free

# 1. .NET 10 SDK
if (Test-Path (Join-Path $DotnetDir 'dotnet.exe')) {
    Write-Host ".NET já instalado em $DotnetDir" -ForegroundColor DarkGray
} else {
    Confirmar '.NET 10 SDK (dotnet-install.ps1)' $Downloads.DotnetInstall
    $script = Join-Path $Tmp 'dotnet-install.ps1'
    Invoke-WebRequest $Downloads.DotnetInstall.Url -OutFile $script
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    & $script -Channel 10.0 -Quality GA -InstallDir $DotnetDir -NoPath
}

# 2. llvm-mingw
if (Test-Path (Join-Path $LlvmDir 'bin\clang++.exe')) {
    Write-Host "llvm-mingw já instalado em $LlvmDir" -ForegroundColor DarkGray
} else {
    Confirmar 'llvm-mingw 20260922 (ucrt x86_64)' $Downloads.Llvm
    $zip = Join-Path $Tmp "$LlvmNome.zip"
    Invoke-WebRequest $Downloads.Llvm.Url -OutFile $zip
    Expand-Archive $zip -DestinationPath $Toolchains -Force
    Remove-Item $zip
}

# 3. C++/WinRT 2.0.250303.1
if (Test-Path (Join-Path $WinrtDir 'bin\cppwinrt.exe')) {
    Write-Host "cppwinrt já instalado em $WinrtDir" -ForegroundColor DarkGray
} else {
    Confirmar 'Microsoft.Windows.CppWinRT 2.0.250303.1' $Downloads.CppWinRT
    $pkg = Join-Path $Tmp 'cppwinrt.zip'
    Invoke-WebRequest $Downloads.CppWinRT.Url -OutFile $pkg
    Expand-Archive $pkg -DestinationPath $WinrtDir -Force
    Remove-Item $pkg
}

$gastoC = [math]::Round(($livreAntes - (Get-PSDrive C).Free) / 1MB)
Write-Host ""
Write-Host "Pronto. Variação de espaço livre no C: durante a instalação: $gastoC MB" -ForegroundColor Green
Write-Host "Para usar: . G:\dev\vitral\spike\tools\env.ps1"
