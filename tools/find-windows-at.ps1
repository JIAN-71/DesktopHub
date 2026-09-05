param([int]$x0, [int]$y0, [int]$x1, [int]$y1)
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WinEnum2 {
  public delegate bool EnumProc(IntPtr hwnd, IntPtr lparam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lparam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder sb, int max);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT r, int size);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern int GetWindowRgn(IntPtr hwnd, IntPtr rgn);
  [DllImport("gdi32.dll")] public static extern int GetRgnBox(IntPtr rgn, out RECT r);
}
"@

$list = New-Object System.Collections.ArrayList
$cb = [WinEnum2+EnumProc]{
  param($h, $l)
  if (-not [WinEnum2]::IsWindowVisible($h)) { return $true }
  $dr = New-Object WinEnum2+RECT
  $hr = [WinEnum2]::DwmGetWindowAttribute($h, 9, [ref]$dr, 16)
  if ($hr -ne 0) {
    $r0 = New-Object WinEnum2+RECT
    [WinEnum2]::GetWindowRect($h, [ref]$r0) | Out-Null
    $dr = $r0
  }
  # 物理坐标相交判定
  if ($dr.R -gt $x0 -and $dr.L -lt $x1 -and $dr.B -gt $y0 -and $dr.T -lt $y1) {
    $wpid = [uint32]0
    [WinEnum2]::GetWindowThreadProcessId($h, [ref]$wpid) | Out-Null
    $proc = Get-Process -Id $wpid -ErrorAction SilentlyContinue
    $t = New-Object System.Text.StringBuilder 256
    [WinEnum2]::GetWindowText($h, $t, 256) | Out-Null
    $c = New-Object System.Text.StringBuilder 256
    [WinEnum2]::GetClassName($h, $c, 256) | Out-Null
    # 窗口 region
    $hrgn = [System.Runtime.InteropServices.Marshal]::AllocHGlobal(64)
    $rgnRes = [WinEnum2]::GetWindowRgn($h, $hrgn)
    $rgnBox = "none"
    if ($rgnRes -ne 0) {
      $rb = New-Object WinEnum2+RECT
      [WinEnum2]::GetRgnBox($hrgn, [ref]$rb) | Out-Null
      $rgnBox = "({0},{1})-({2},{3})" -f $rb.L, $rb.T, $rb.R, $rb.B
    }
    [System.Runtime.InteropServices.Marshal]::FreeHGlobal($hrgn)
    $rectStr = "({0},{1})-({2},{3}) {4}x{5}" -f $dr.L, $dr.T, $dr.R, $dr.B, ($dr.R - $dr.L), ($dr.B - $dr.T)
    $exstyle = [WinEnum2]::GetWindowLong($h, -20)
    $null = $list.Add([pscustomobject]@{
      Hwnd = $h
      Proc = $proc.ProcessName
      Pid2 = $wpid
      Class = $c.ToString()
      Title = $t.ToString()
      PhysRect = $rectStr
      Rgn = $rgnBox
      ExStyle = "0x{0:X}" -f $exstyle
      Cloaked = ""
    })
  }
  return $true
}
[WinEnum2]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$list | Format-Table -AutoSize | Out-String -Width 260
