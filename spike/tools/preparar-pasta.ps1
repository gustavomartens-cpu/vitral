# Dá a processos empacotados (Iniciar, central) leitura da DLL e escrita nos logs.
# S-1-15-2-1 = ALL APPLICATION PACKAGES; S-1-15-2-2 = ALL RESTRICTED APPLICATION PACKAGES.
$ErrorActionPreference = 'Stop'
$out  = Join-Path $PSScriptRoot '..\out' | Resolve-Path
$logs = Join-Path $out 'logs'
New-Item -ItemType Directory -Force $logs | Out-Null
foreach ($sid in '*S-1-15-2-1', '*S-1-15-2-2') {
    icacls $out  /grant "${sid}:(OI)(CI)(RX)" | Out-Null
    icacls $logs /grant "${sid}:(OI)(CI)(M)"  | Out-Null
}
icacls $out | Select-String 'S-1-15-2|APPLICATION PACKAGES|PACOTES DE APLICATIVOS'
