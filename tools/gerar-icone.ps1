# Gera o src/Vitral.App/Recursos/vitral.ico a partir do Logo.xaml (spec do ajuste visual §5).
# 16 e 24 px usam o LogoPequeno (contorno mais grosso); 32, 48, 64 e 256 px, o Logo. Cada tamanho vai como PNG dentro do ICO.
# Uso: pwsh -NoProfile -File tools/gerar-icone.ps1   (roda o desenho numa thread STA, que o WPF exige)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$xaml = Join-Path $raiz 'src\Vitral.App\Tema\Logo.xaml'
$saida = Join-Path $raiz 'src\Vitral.App\Recursos\vitral.ico'

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$desenhar = {
    param($xaml, $saida)
    $dic = [System.Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText($xaml))
    $tamanhos = 16, 24, 32, 48, 64, 256
    $pngs = foreach ($t in $tamanhos) {
        $imagem = if ($t -le 24) { $dic['LogoPequeno'] } else { $dic['Logo'] }
        $visual = [System.Windows.Media.DrawingVisual]::new()
        $ctx = $visual.RenderOpen()
        $ctx.DrawImage($imagem, [System.Windows.Rect]::new(0, 0, $t, $t))
        $ctx.Close()
        $bmp = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($t, $t, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
        $bmp.Render($visual)
        $enc = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
        $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
        $ms = [IO.MemoryStream]::new()
        $enc.Save($ms)
        [pscustomobject]@{ Tamanho = $t; Dados = $ms.ToArray() }
    }

    # ICONDIR (6 bytes) + uma ICONDIRENTRY (16 bytes) por tamanho + os PNGs
    $ms = [IO.MemoryStream]::new()
    $w = [IO.BinaryWriter]::new($ms)
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$pngs.Count)
    $deslocamento = 6 + 16 * $pngs.Count
    foreach ($p in $pngs) {
        $lado = if ($p.Tamanho -ge 256) { 0 } else { $p.Tamanho }
        $w.Write([byte]$lado); $w.Write([byte]$lado); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32)
        $w.Write([uint32]$p.Dados.Length); $w.Write([uint32]$deslocamento)
        $deslocamento += $p.Dados.Length
    }
    foreach ($p in $pngs) { $w.Write($p.Dados) }
    $w.Flush()
    [IO.File]::WriteAllBytes($saida, $ms.ToArray())
    foreach ($p in $pngs) { "{0,4} px  {1,7} bytes" -f $p.Tamanho, $p.Dados.Length }
    "Gravado: $saida ($($ms.Length) bytes)"
}

# O WPF precisa de STA: roda o desenho num runspace STA.
$rs = [runspacefactory]::CreateRunspace()
$rs.ApartmentState = 'STA'
$rs.Open()
$ps = [powershell]::Create()
$ps.Runspace = $rs
$null = $ps.AddScript($desenhar).AddArgument($xaml).AddArgument($saida)
try { $ps.Invoke() | ForEach-Object { $_ }; if ($ps.HadErrors) { $ps.Streams.Error | ForEach-Object { throw $_ } } }
finally { $ps.Dispose(); $rs.Dispose() }
