# Prova da central (DESCARTÁVEL): conecta o componente da fase 0 em modo só-registro a ShellExperienceHost e
# ShellHost, abre a central (Win+N) e as configurações rápidas (Win+A) e mostra quem registrou o quê.
$ErrorActionPreference = 'Stop'
Add-Type -Namespace V -Name Tecla -MemberDefinition '[DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, UIntPtr e);'
function Combo([byte] $letra) {
    [V.Tecla]::keybd_event(0x5B, 0, 0, [UIntPtr]::Zero); [V.Tecla]::keybd_event($letra, 0, 0, [UIntPtr]::Zero)
    [V.Tecla]::keybd_event($letra, 0, 2, [UIntPtr]::Zero); [V.Tecla]::keybd_event(0x5B, 0, 2, [UIntPtr]::Zero)
}
function Esc { [V.Tecla]::keybd_event(0x1B, 0, 0, [UIntPtr]::Zero); [V.Tecla]::keybd_event(0x1B, 0, 2, [UIntPtr]::Zero) }
$sessao = (Get-Process -Id $PID).SessionId
$injetor = Join-Path $PSScriptRoot 'injector\bin\Release\net10.0-windows\Injector.exe'
$alvos = 'ShellExperienceHost', 'ShellHost' | ForEach-Object { Get-Process $_ | Where-Object SessionId -eq $sessao | Select-Object -First 1 }
$hosts = foreach ($a in $alvos) {
    Start-Process $injetor -ArgumentList "--rota externa --alvo iniciar --modo log --pid $($a.Id) --segurar 40" -PassThru -WindowStyle Hidden
}
Start-Sleep 8
Combo 0x4E; Start-Sleep 3; Esc; Start-Sleep 2   # Win+N: central de notificações
Combo 0x41; Start-Sleep 3; Esc; Start-Sleep 2   # Win+A: configurações rápidas
$hosts | ForEach-Object { $_.WaitForExit(60000) | Out-Null }
foreach ($a in $alvos) {
    $log = Join-Path $PSScriptRoot "out\logs\tap-$($a.Id).log"
    "== $($a.ProcessName) ($($a.Id)): $((Get-Content $log -ErrorAction SilentlyContinue).Count) linhas"
}
