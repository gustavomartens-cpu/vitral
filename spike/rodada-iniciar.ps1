# Uma rodada da prova da fase 2 (DESCARTÁVEL): [reinicia o Iniciar], abre, conecta o IniciarProva.dll, fotografa a tela
# com ele pintado, deixa o injector sair (o componente desfaz) e fotografa de novo. Resultado em resultados/iniciar.csv.
param([int] $Rodada = 1, [switch] $Reiniciar, [string] $Fotos = "$env:TEMP")
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

$spike = $PSScriptRoot
if ($Reiniciar) {
    Get-Process StartMenuExperienceHost -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep 2
}
Win; Start-Sleep 3 # abrir o Iniciar sobe o processo e monta a interface
$injetor = Join-Path $spike 'injector\bin\Release\net10.0-windows\Injector.exe'
$p = Start-Process $injetor -ArgumentList '--rota externa --alvo iniciar --modo log --dll IniciarProva.dll --segurar 6' -PassThru -WindowStyle Hidden
Start-Sleep 4
Win; Start-Sleep 1; Win; Start-Sleep 2 # fecha e reabre: o fundo pintado aparece numa abertura nova também
Foto "iniciar-$Rodada-pintado.png"
$p.WaitForExit(60000) | Out-Null
Start-Sleep 2
Esc; Start-Sleep 1; Win; Start-Sleep 2
Foto "iniciar-$Rodada-desfeito.png"
Esc
$pid_ = (Get-Process StartMenuExperienceHost).Id
$log = Join-Path $spike "out\logs\iniciar-$pid_.log"
$texto = if (Test-Path $log) { Get-Content $log -Raw } else { '' }
$res = [pscustomobject]@{
    Rodada = $Rodada; Reiniciou = [bool]$Reiniciar; Saida = $p.ExitCode
    Advise = $texto -match 'ADVISE\|hr=0'; Pintou = ([regex]::Matches($texto, 'PINTADO\|')).Count; Desfez = ([regex]::Matches($texto, 'DESFEITO')).Count
}
"$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss');$($res.Rodada);$($res.Reiniciou);$($res.Saida);$($res.Advise);$($res.Pintou);$($res.Desfez)" |
    Add-Content (Join-Path $spike 'resultados\iniciar.csv')
$res
