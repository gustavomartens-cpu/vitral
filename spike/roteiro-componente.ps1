# Roteiro da Task 3 do plano do Iniciar (DESCARTÁVEL): conecta o VitralIniciar.dll pelo injector da prova e fotografa
# a borda, a troca ao vivo do arquivo de cores, o "ligado":false e a saída do host.
param([string] $Fotos = "$env:TEMP", [int] $Segurar = 40)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -Namespace V -Name Tecla -MemberDefinition '[DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, UIntPtr e);'
function Win { [V.Tecla]::keybd_event(0x5B, 0, 0, [UIntPtr]::Zero); [V.Tecla]::keybd_event(0x5B, 0, 2, [UIntPtr]::Zero) }
function Esc { [V.Tecla]::keybd_event(0x1B, 0, 0, [UIntPtr]::Zero); [V.Tecla]::keybd_event(0x1B, 0, 2, [UIntPtr]::Zero) }
function Foto([string] $nome) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = [System.Drawing.Bitmap]::new($b.Width, $b.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $bmp.Save((Join-Path $Fotos $nome), [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
}
function Cores([string] $json) { Set-Content (Join-Path $PSScriptRoot 'out\cores\cores.json.tmp') $json -NoNewline -Encoding ascii
    Move-Item (Join-Path $PSScriptRoot 'out\cores\cores.json.tmp') (Join-Path $PSScriptRoot 'out\cores\cores.json') -Force }

Get-Process StartMenuExperienceHost -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep 2
Cores '{"ligado":true,"vidro":true,"tinta":"#CC0A0F20","borda":"#FFFF0000","raio":20}'
Win; Start-Sleep 3
$injetor = Join-Path $PSScriptRoot 'injector\bin\Release\net10.0-windows\Injector.exe'
$cores = Join-Path $PSScriptRoot 'out\cores\cores.json'
$p = Start-Process $injetor -ArgumentList "--rota externa --alvo iniciar --modo log --dll VitralIniciar.dll --clsid 9C3E7A14-2B6D-4F1E-8A53-61D0C47B2E95 --cores `"$cores`" --segurar $Segurar" -PassThru -WindowStyle Hidden
Start-Sleep 5
Win; Start-Sleep 1; Win; Start-Sleep 2; Foto 'comp-1-borda.png'
Cores '{"ligado":true,"vidro":false,"tinta":"#FF5A3A08","borda":"#FFFFFFFF","raio":4}'
Start-Sleep 1.5; Foto 'comp-2-aovivo.png'
Cores '{"ligado":false}'
Start-Sleep 1.5; Foto 'comp-3-desligado.png'
Cores '{"ligado":true,"vidro":true,"tinta":"#CC0A0F20","borda":"#FFFF0000","raio":20}'
Start-Sleep 1.5; Esc
$p.WaitForExit(120000) | Out-Null
Start-Sleep 2; Win; Start-Sleep 2; Foto 'comp-4-host-saiu.png'; Esc
$pid_ = (Get-Process StartMenuExperienceHost).Id
Get-Content (Join-Path $PSScriptRoot "out\cores\logs\iniciar-$pid_.log")
