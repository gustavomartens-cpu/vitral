# Ativa as ferramentas da fase 0 só neste processo (rode com ". .\spike\tools\env.ps1").
$env:DOTNET_ROOT                 = 'G:\dev\dotnet'
$env:DOTNET_CLI_HOME             = 'G:\dev\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO               = '1'
$env:NUGET_PACKAGES              = 'G:\dev\nuget\packages'
$env:NUGET_HTTP_CACHE_PATH       = 'G:\dev\nuget\http'
$env:NUGET_PLUGINS_CACHE_PATH    = 'G:\dev\nuget\plugins'

$extra = @(
    'G:\dev\dotnet',
    'G:\dev\toolchains\llvm-mingw-20260922-ucrt-x86_64\bin',
    'G:\dev\toolchains\cppwinrt\bin',
    'G:\dev\dotnet-tools'
)
$env:PATH = (($extra + ($env:PATH -split ';')) | Where-Object { $_ } | Select-Object -Unique) -join ';'
