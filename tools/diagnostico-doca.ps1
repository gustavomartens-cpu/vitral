# Diagnóstico da doca: escala, área de trabalho, barras do Explorer e estado gravado pelo Vitral.
# Só lê; não muda nada no Windows. Rodar com o Vitral aberto e a doca ligada, com uma janela maximizada:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\diagnostico-doca.ps1 | Set-Clipboard
# e colar a saída na conversa.

$ErrorActionPreference = 'Continue'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class DiagDoca
{
    [StructLayout(LayoutKind.Sequential)] public struct Ret { public int Esq, Topo, Dir, Base; public override string ToString() { return string.Format("({0},{1})-({2},{3}) {4}x{5}", Esq, Topo, Dir, Base, Dir - Esq, Base - Topo); } }
    [StructLayout(LayoutKind.Sequential)] public struct Ponto { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct InfoMonitor { public uint Tamanho; public Ret Monitor, Trabalho; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] public struct AppBarData { public uint Tamanho; public IntPtr Janela; public uint MensagemRetorno; public uint Borda; public Ret Retangulo; public IntPtr Parametro; }

    [DllImport("user32.dll")] public static extern IntPtr SetProcessDpiAwarenessContext(IntPtr contexto);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(Ponto p, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr monitor, ref InfoMonitor info);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int tipo, out uint x, out uint y);
    [DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint acao, uint p1, ref Ret p2, uint f);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string classe, string titulo);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr pai, IntPtr depois, string classe, string titulo);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Ret r);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("shell32.dll")] public static extern UIntPtr SHAppBarMessage(uint msg, ref AppBarData d);
    public delegate bool Enumerador(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Enumerador f, IntPtr p);

    public static string Classe(IntPtr h) { var s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }
    public static string Titulo(IntPtr h) { var s = new StringBuilder(256); GetWindowText(h, s, 256); return s.ToString(); }
    public static uint Pid(IntPtr h) { uint p; GetWindowThreadProcessId(h, out p); return p; }
    public static Ret Retangulo(IntPtr h) { Ret r; GetWindowRect(h, out r); return r; }
    public static bool NoTopo(IntPtr h) { return (GetWindowLong(h, -20) & 0x8) != 0; }

    public static List<IntPtr> PorClasse(string classe)
    {
        var l = new List<IntPtr>(); IntPtr h = IntPtr.Zero;
        while ((h = FindWindowEx(IntPtr.Zero, h, classe, null)) != IntPtr.Zero) l.Add(h);
        return l;
    }

    // Janelas visíveis, na ordem Z (a de cima primeiro).
    public static List<IntPtr> Visiveis()
    {
        var l = new List<IntPtr>();
        EnumWindows((h, p) => { if (IsWindowVisible(h)) l.Add(h); return true; }, IntPtr.Zero);
        return l;
    }

    public static AppBarData Dados(IntPtr janela) { var d = new AppBarData(); d.Tamanho = (uint)Marshal.SizeOf(typeof(AppBarData)); d.Janela = janela; return d; }
}
'@

function Processo([uint32]$id) {
    $p = Get-Process -Id $id -ErrorAction SilentlyContinue
    if ($p) { "$($p.ProcessName) (PID $id)" } else { "PID $id" }
}

"=== Vitral: diagnóstico da doca ($(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')) ==="
$os = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction SilentlyContinue
"Windows: $($os.DisplayVersion) build $($os.CurrentBuild).$($os.UBR)"

# Por monitor V2 (-4): retângulos em pixels físicos, como o Vitral vê.
$ok = [DiagDoca]::SetProcessDpiAwarenessContext([IntPtr](-4)) -ne [IntPtr]::Zero
"Leitura em pixels físicos (PerMonitorV2): $ok"

"`n=== Monitor principal ==="
$mon = [DiagDoca]::MonitorFromPoint((New-Object DiagDoca+Ponto), 1)
$info = New-Object DiagDoca+InfoMonitor
$info.Tamanho = [System.Runtime.InteropServices.Marshal]::SizeOf([type][DiagDoca+InfoMonitor])
[void][DiagDoca]::GetMonitorInfo($mon, [ref]$info)
[uint32]$dx = 0; [uint32]$dy = 0
[void][DiagDoca]::GetDpiForMonitor($mon, 0, [ref]$dx, [ref]$dy)
$escala = $dx / 96.0
"DPI efetivo: $dx (escala $([math]::Round($escala * 100))%)"
"Monitor:            $($info.Monitor)"
"Área de trabalho:   $($info.Trabalho)"
$wa = New-Object DiagDoca+Ret
[void][DiagDoca]::SystemParametersInfo(0x30, 0, [ref]$wa, 0)
"SPI_GETWORKAREA:    $wa"
$sobraBaixo = $info.Monitor.Base - $info.Trabalho.Base
$sobraTopo = $info.Trabalho.Topo - $info.Monitor.Topo
"Reservado embaixo: $sobraBaixo px = $([math]::Round($sobraBaixo / $escala, 1)) DIPs (a doca sozinha: 62 DIPs = $([math]::Round(62 * $escala)) px)"
"Reservado no topo: $sobraTopo px = $([math]::Round($sobraTopo / $escala, 1)) DIPs (a barra do topo: 46 DIPs = $([math]::Round(46 * $escala)) px)"
Add-Type -AssemblyName System.Windows.Forms
foreach ($s in [System.Windows.Forms.Screen]::AllScreens) { "Tela: $($s.DeviceName) principal=$($s.Primary) limites=$($s.Bounds) trabalho=$($s.WorkingArea)" }

"`n=== Barras Shell_TrayWnd / Shell_SecondaryTrayWnd (ordem do FindWindowEx) ==="
foreach ($classe in 'Shell_TrayWnd', 'Shell_SecondaryTrayWnd') {
    foreach ($h in [DiagDoca]::PorClasse($classe)) {
        "$classe 0x$($h.ToString('X')): $(Processo ([DiagDoca]::Pid($h))) visível=$([DiagDoca]::IsWindowVisible($h)) noTopo=$([DiagDoca]::NoTopo($h)) ret=$([DiagDoca]::Retangulo($h)) título='$([DiagDoca]::Titulo($h))'"
    }
}
$primeira = [DiagDoca]::FindWindow('Shell_TrayWnd', $null)
"FindWindow('Shell_TrayWnd') (quem recebe o SHAppBarMessage): 0x$($primeira.ToString('X')) $(Processo ([DiagDoca]::Pid($primeira)))"

"`n=== Estado da barra (SHAppBarMessage) ==="
# Com a doca ligada, a bandeja do Vitral (ManagedShell) fica na frente e recebe as mensagens: ela só repassa o
# ABM_GETSTATE ao Explorer quando a pergunta é sobre a barra do Explorer.
$explorer = [DiagDoca]::PorClasse('Shell_TrayWnd') | Where-Object { (Get-Process -Id ([DiagDoca]::Pid($_)) -ErrorAction SilentlyContinue).ProcessName -eq 'explorer' } | Select-Object -First 1
foreach ($alvo in @(@('Explorer', $explorer), @('primeira Shell_TrayWnd', $primeira))) {
    if (-not $alvo[1]) { "ABM_GETSTATE ($($alvo[0])): sem janela"; continue }
    $d = [DiagDoca]::Dados($alvo[1])
    $estado = [uint64][DiagDoca]::SHAppBarMessage(4, [ref]$d)
    "ABM_GETSTATE ($($alvo[0])): $estado (ocultar automaticamente=$(($estado -band 1) -ne 0))"
}
$d = [DiagDoca]::Dados($primeira)
[void][DiagDoca]::SHAppBarMessage(5, [ref]$d)
"ABM_GETTASKBARPOS: borda=$($d.Borda) ret=$($d.Retangulo)"
foreach ($borda in 0..3) {
    $d = [DiagDoca]::Dados($primeira); $d.Borda = [uint32]$borda
    $h = [IntPtr][int64][uint64][DiagDoca]::SHAppBarMessage(7, [ref]$d)
    if ($h -ne [IntPtr]::Zero) { "AppBar oculta automaticamente na borda $borda (0=esq 1=topo 2=dir 3=baixo): 0x$($h.ToString('X')) $(Processo ([DiagDoca]::Pid($h)))" }
}

"`n=== StuckRects3 ==="
$sr = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3' -Name Settings -ErrorAction SilentlyContinue).Settings
if ($sr) {
    "Settings: $(($sr | ForEach-Object { $_.ToString('X2') }) -join ' ')"
    "Byte 8: 0x$($sr[8].ToString('X2')) (ocultar automaticamente=$(($sr[8] -band 1) -ne 0))"
} else { "Settings: (não existe)" }
$adv = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -ErrorAction SilentlyContinue
"TaskbarSi (tamanho da barra): $($adv.TaskbarSi)   TaskbarAl (alinhamento): $($adv.TaskbarAl)"

"`n=== Janelas do Vitral e maximizadas ==="
foreach ($h in [DiagDoca]::Visiveis()) {
    $pid_ = [DiagDoca]::Pid($h)
    $nome = (Get-Process -Id $pid_ -ErrorAction SilentlyContinue).ProcessName
    $max = [DiagDoca]::IsZoomed($h)
    if ($nome -eq 'Vitral' -or $max) {
        "$(if ($max) { 'MAXIMIZADA' } else { 'vitral' }) $nome classe=$([DiagDoca]::Classe($h)) dpi=$([DiagDoca]::GetDpiForWindow($h)) noTopo=$([DiagDoca]::NoTopo($h)) ret=$([DiagDoca]::Retangulo($h)) título='$([DiagDoca]::Titulo($h))'"
    }
}
$p = Get-Process Vitral -ErrorAction SilentlyContinue
if ($p) { foreach ($x in $p) { "Processo Vitral PID $($x.Id) iniciado $($x.StartTime) caminho=$($x.Path) versão=$($x.MainModule.FileVersionInfo.ProductVersion)" } }
else { "Processo Vitral: não está rodando" }

"`n=== %LOCALAPPDATA%\Vitral\config.json ==="
$cfg = Join-Path $env:LOCALAPPDATA 'Vitral\config.json'
if (Test-Path $cfg) {
    $json = Get-Content $cfg -Raw
    try {
        $c = $json | ConvertFrom-Json
        "barraBaixo: $($c.barraBaixo | ConvertTo-Json -Compress)"
        "ocultarAutomaticoAntesDaDoca: $($c.ocultarAutomaticoAntesDaDoca)"
        "barraTopo: $($c.barraTopo | ConvertTo-Json -Compress)"
    } catch { "não li o JSON: $_" }
    "--- inteiro ---"
    $json
} else { "(não existe)" }

"`n=== Fim do barra.log (40 linhas) ==="
$log = Join-Path $env:LOCALAPPDATA 'Vitral\logs\barra.log'
if (Test-Path $log) { Get-Content $log -Tail 40 } else { "(não existe)" }
