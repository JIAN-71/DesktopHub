param([long]$hwnd)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class PW2 {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
}
'@
[void][PW2]::SetProcessDPIAware()

$el = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$hwnd)
$rfCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::BoundingRectangleProperty,
    (New-Object System.Windows.Rect 1609, 38, 39, 39))
$refresh = $el.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $rfCond)
if ($null -eq $refresh) { Write-Output 'refresh not found'; exit 1 }

$refresh.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()

for ($i = 0; $i -lt 4; $i++) {
  $bmp = New-Object System.Drawing.Bitmap 960, 870
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  [PW2]::PrintWindow([IntPtr]$hwnd, $hdc, 2) | Out-Null
  $g.ReleaseHdc($hdc)
  $g.Dispose()
  # 头部图标区(窗口坐标 x≈870..960, y≈8..80)
  $crop = $bmp.Clone((New-Object System.Drawing.Rectangle 780, 8, 170, 72), $bmp.PixelFormat)
  $crop.Save(("D:/Project/DesktopHub/.workbuddy/diagnostics/rect-fix/spin-{0}.png" -f $i), [System.Drawing.Imaging.ImageFormat]::Png)
  $crop.Dispose(); $bmp.Dispose()
  Start-Sleep -Milliseconds 130
}
Write-Output 'captured 4 frames'
