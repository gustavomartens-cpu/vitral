# Prova WhatsApp (DESCARTÁVEL): despeja a árvore de UI Automation da janela do WhatsApp. Rode no Windows PowerShell 5.1.
param([int]$Profundidade = 12, [int]$Max = 600)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
$p = Get-Process WhatsApp.Root -ErrorAction Stop | ? MainWindowHandle -ne 0 | Select -First 1
$raiz = $A::FromHandle($p.MainWindowHandle)
$w = [System.Windows.Automation.TreeWalker]::RawViewWalker
$script:n = 0
function Ver($e, $d) {
  if ($script:n -ge $Max -or $d -gt $Profundidade) { return }
  $script:n++
  $c = $e.Current
  $nome = if ($c.Name.Length -gt 70) { $c.Name.Substring(0,70) + '…' } else { $c.Name }
  "{0}{1} '{2}' id={3} cls={4} fw={5}" -f ('  ' * $d), $c.ControlType.ProgrammaticName.Replace('ControlType.',''), $nome, $c.AutomationId, $c.ClassName, $c.FrameworkId
  $f = $w.GetFirstChild($e)
  while ($f) { Ver $f ($d + 1); $f = $w.GetNextSibling($f) }
}
Ver $raiz 0
