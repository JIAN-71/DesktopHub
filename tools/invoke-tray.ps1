Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement
# 找 DesktopHub 托盘图标(UIA Name = ToolTipText);限定 Button/MenuItem 控件角色由 InvokePattern 判定
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, 'DesktopHub - 桌面图标面板')
$el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
if ($null -eq $el) { Write-Output 'NOT FOUND'; exit 1 }

Write-Output ("found: " + $el.Current.Name + " | " + $el.Current.ControlType.ProgrammaticName + " | " + $el.Current.ClassName)
$invoke = $null
if ($el.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) {
    $invoke.Invoke()
    Write-Output 'INVOKED'
} elseif ($el.TryGetCurrentPattern([System.Windows.Automation.LegacyIAccessiblePattern]::Pattern, [ref]$legacy)) {
    Write-Output 'no InvokePattern; legacy DoDefaultAction'
    $legacy.DoDefaultAction()
    Write-Output 'DONE'
} else {
    Write-Output 'NO INVOKE PATTERN'
}
