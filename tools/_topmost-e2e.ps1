param()
# 端到端验证「顶置胶囊岛」开关:物理点击展开胶囊 → UIA 点设置 → 切开关 → 保存 → 校验 WS_EX_TOPMOST 与 config.json
Add-Type -AssemblyName UIAutomationClient
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class TP {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public struct RECT { public int L, T, R, B; }
}
'@
[void][TP]::SetProcessDPIAware()

$proc = Get-Process DesktopHub.App | Select-Object -First 1
if (-not $proc) { Write-Output 'FAIL: app not running'; exit 1 }
$pid32 = [uint32]$proc.Id

function Find-Window([string]$title) {
  $script:found = [IntPtr]::Zero
  $cb = [TP+EnumProc]{ param($h, $l)
    $wpid = 0
    [void][TP]::GetWindowThreadProcessId($h, [ref]$wpid)
    if ($wpid -eq $pid32 -and [TP]::IsWindowVisible($h)) {
      $sb = New-Object System.Text.StringBuilder 256
      [void][TP]::GetWindowText($h, $sb, 256)
      if ($sb.ToString() -eq $title) { $script:found = $h; return $false }
    }
    return $true
  }
  [void][TP]::EnumWindows($cb, [IntPtr]::Zero)
  return $script:found
}

function Is-Topmost([IntPtr]$h) { return (([TP]::GetWindowLong($h, -20) -band 0x8) -ne 0) }

$pill = Find-Window 'DesktopHub'
if ($pill -eq [IntPtr]::Zero) { Write-Output 'FAIL: pill window not found'; exit 1 }
Write-Output ("step1 default topmost (expect True): " + (Is-Topmost $pill))

# 展开胶囊并点设置(UIA Invoke,无需精准物理点击)
$r = New-Object TP+RECT
[TP]::GetWindowRect($pill, [ref]$r) | Out-Null
$cx = [int](($r.L + $r.R) / 2); $cy = [int](($r.T + $r.B) / 2)
[TP]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 500
[TP]::mouse_event(2,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 70; [TP]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 900

$root = [System.Windows.Automation.AutomationElement]::FromHandle($pill)
$allBtn = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
$settingsBtn = $null
foreach ($b in $allBtn) { if ($b.Current.HelpText -eq '设置') { $settingsBtn = $b; break } }
if (-not $settingsBtn) {
  # 兜底:展开面板头部按钮依次为 刷新/隐藏图标/设置,取最后一个 HelpText 非空的头部按钮
  $head = @($allBtn | Where-Object { $_.Current.IsOffscreen -eq $false })
  if ($head.Count -gt 0) { $settingsBtn = $head[-1] }
}
if (-not $settingsBtn) { Write-Output ('FAIL: settings button not found; buttons: ' + (($allBtn | ForEach-Object { "[$($_.Current.Name)/$($_.Current.HelpText)]" }) -join ' ')); exit 1 }
$settingsBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 900

$sw = Find-Window 'DesktopHub 设置'
if ($sw -eq [IntPtr]::Zero) { Write-Output 'FAIL: settings window not found'; exit 1 }
$sroot = [System.Windows.Automation.AutomationElement]::FromHandle($sw)

# 先切到「通用」分区:非当前分区的 CheckBox 为 Collapsed(不在 UIA 树中)
$navItems = $sroot.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))
$navGeneral = $null
foreach ($n in $navItems) { if ($n.Current.Name -like '*通用*') { $navGeneral = $n; break } }
# ListBoxItem 内容是 StackPanel,UIA Name 常退化为类型名;导航顺序固定为 个性化/通用/分组规则,按序号取
if (-not $navGeneral -and $navItems.Count -ge 2) { $navGeneral = $navItems[1] }
if (-not $navGeneral) { Write-Output 'FAIL: 通用 nav not found'; exit 1 }
$navGeneral.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Start-Sleep -Milliseconds 500

$allCb = $sroot.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::CheckBox)))
$target = $null
foreach ($cb in $allCb) { if ($cb.Current.Name -like '顶置胶囊岛*') { $target = $cb; break } }
if (-not $target) { Write-Output ('FAIL: checkbox not found; checkboxes: ' + (($allCb | ForEach-Object { $_.Current.Name }) -join ' | ')); exit 1 }
Write-Output ("step2 checkbox found, initial IsOn: " + $target.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState)
$target.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()

$save = $sroot.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '保存并应用')))
if (-not $save) { Write-Output 'FAIL: save button not found'; exit 1 }
$save.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 900

Write-Output ("step3 after save topmost (expect False): " + (Is-Topmost $pill))
Write-Output ("step4 config pillTopmost: " + (Get-Content "$env:APPDATA\DesktopHub\config.json" -Raw | ConvertFrom-Json).pillTopmost)

# 关闭设置窗口(避免挡后续)
[void][TP]::PostMessage($sw, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
