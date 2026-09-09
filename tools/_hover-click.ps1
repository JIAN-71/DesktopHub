param([long]$hwnd, [int]$mode)  # mode 0=悬停, 1=悬停+点击展开, 2=移开
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class INJ {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
  public struct RECT { public int L, T, R, B; }
}
'@
[void][INJ]::SetProcessDPIAware()
$r = New-Object INJ+RECT
[INJ]::GetWindowRect([IntPtr]$hwnd, [ref]$r) | Out-Null
$cx = [int](($r.L + $r.R) / 2); $cy = [int](($r.T + $r.B) / 2)

switch ($mode) {
  2 { [INJ]::SetCursorPos($cx, 500) | Out-Null; Write-Output "moved away to $cx,500" }
  0 { [INJ]::SetCursorPos($cx, $cy) | Out-Null; Write-Output "hover at $cx,$cy" }
  1 {
    [INJ]::SetCursorPos($cx, $cy) | Out-Null
    Start-Sleep -Milliseconds 400   # 等悬停展开(Recent)完成再点击
    [INJ]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)   # LEFTDOWN
    Start-Sleep -Milliseconds 60
    [INJ]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)   # LEFTUP
    Write-Output "clicked at $cx,$cy"
  }
}
