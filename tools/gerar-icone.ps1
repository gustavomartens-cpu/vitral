# Gera src/Vitral.App/Recursos/vitral.ico: losango de vitral com 4 gomos âmbar/azul, fundo transparente.
# Mesmo desenho de src/Vitral.App/Tema/Logo.xaml (spec 2026-10-24 §2.2).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$ambar = [System.Drawing.Color]::FromArgb(255, 0xF2, 0xB5, 0x44)
$azul  = [System.Drawing.Color]::FromArgb(255, 0x4C, 0x7D, 0xF0)

function Png([int] $s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.TranslateTransform($s / 2, $s / 2); $g.RotateTransform(45); $g.TranslateTransform(-$s / 2, -$s / 2)
    $u = $s / 100.0                        # unidades do Logo.xaml (0..100)
    $gomo = 30.5 * $u; $r = [Math]::Max(1, 4 * $u)
    $cantos = @(@(18, 18, $ambar), @(51.5, 18, $azul), @(18, 51.5, $azul), @(51.5, 51.5, $ambar))
    foreach ($c in $cantos) {
        $x = $c[0] * $u; $y = $c[1] * $u
        $p = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = 2 * $r
        $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $gomo - $d, $y, $d, $d, 270, 90)
        $p.AddArc($x + $gomo - $d, $y + $gomo - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $gomo - $d, $d, $d, 90, 90); $p.CloseFigure()
        $g.FillPath((New-Object System.Drawing.SolidBrush $c[2]), $p)
    }
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
    , $ms.ToArray()
}

$tamanhos = 16, 24, 32, 48, 256
$imagens = $tamanhos | ForEach-Object { , (Png $_) }
$destino = Join-Path $PSScriptRoot '..\src\Vitral.App\Recursos\vitral.ico'
$fs = [System.IO.File]::Create((Resolve-Path (Split-Path $destino)).Path + '\vitral.ico')
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$tamanhos.Count)
$offset = 6 + 16 * $tamanhos.Count
for ($i = 0; $i -lt $tamanhos.Count; $i++) {
    $s = $tamanhos[$i]
    $w.Write([byte]($s % 256)); $w.Write([byte]($s % 256)); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$imagens[$i].Length); $w.Write([uint32]$offset)
    $offset += $imagens[$i].Length
}
foreach ($b in $imagens) { $w.Write($b) }
$w.Close()
Write-Host "Ícone gerado: $destino"
