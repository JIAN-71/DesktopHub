Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement
$desc = [System.Windows.Automation.TreeScope]::Descendants
$buttonType = [System.Windows.Automation.ControlType]::Button

foreach ($cls in @('Shell_TrayWnd', 'NotifyIconOverflowWindow')) {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ClassNameProperty, $cls)
    $win = $root.FindFirst($desc, $cond)
    if ($null -eq $win) { Write-Output "$cls : NOT FOUND"; continue }
    Write-Output "== $cls found =="
    $btnCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $buttonType)
    $buttons = $win.FindAll($desc, $btnCond)
    foreach ($b in $buttons) {
        Write-Output ("  btn: '" + $b.Current.Name + "' class=" + $b.Current.ClassName)
    }
}
