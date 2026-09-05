param([string]$sectionName, [long]$hwnd)
Add-Type -AssemblyName UIAutomationClient
$settings = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$hwnd)
if ($null -eq $settings) { Write-Output 'SETTINGS WINDOW NOT FOUND'; exit 1 }

# 按名称找文本元素(ListBoxItem 的 Name 可能为空,图标+文本组合内容)
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $sectionName)
$el = $settings.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
if ($null -eq $el) { Write-Output 'TEXT ELEMENT NOT FOUND'; exit 1 }

# 沿祖先链找到支持 SelectionItemPattern 的容器(ListBoxItem)
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$node = $el
while ($null -ne $node) {
    $sel = $null
    if ($node.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$sel)) {
        $sel.Select()
        Write-Output ("SELECTED ok")
        exit 0
    }
    $node = $walker.GetParent($node)
}
Write-Output 'NO SELECTABLE ANCESTOR'; exit 1
