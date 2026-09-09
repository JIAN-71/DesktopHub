param([int]$pid2, [int]$minWidth)
Add-Type -AssemblyName UIAutomationClient
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $pid2)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
foreach ($w in $wins) {
  if ([int]$w.Current.BoundingRectangle.Width -ge $minWidth) {
    Write-Output ("hwnd=" + $w.Current.NativeWindowHandle + " " + [int]$w.Current.BoundingRectangle.Width + "x" + [int]$w.Current.BoundingRectangle.Height)
  }
}
