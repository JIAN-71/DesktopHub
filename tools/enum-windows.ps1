Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WinEnum {
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
}
"@

$targetPid = [uint32]$args[0]
$list = New-Object System.Collections.ArrayList
$cb = [WinEnum+EnumProc]{
  param($h, $l)
  $wpid = [uint32]0
  [WinEnum]::GetWindowThreadProcessId($h, [ref]$wpid) | Out-Null
  if ($wpid -eq $targetPid) {
    $r = New-Object WinEnum+RECT
    [WinEnum]::GetWindowRect($h, [ref]$r) | Out-Null
    $dr = New-Object WinEnum+RECT
    $hr = [WinEnum]::DwmGetWindowAttribute($h, 9, [ref]$dr, 16)
    $t = New-Object System.Text.StringBuilder 256
    [WinEnum]::GetWindowText($h, $t, 256) | Out-Null
    $c = New-Object System.Text.StringBuilder 256
    [WinEnum]::GetClassName($h, $c, 256) | Out-Null
    $style = [WinEnum]::GetWindowLong($h, -16)
    $exstyle = [WinEnum]::GetWindowLong($h, -20)
    $getRect = "({0},{1})-({2},{3}) {4}x{5}" -f $r.L, $r.T, $r.R, $r.B, ($r.R - $r.L), ($r.B - $r.T)
    $dwmRect = "({0},{1})-({2},{3}) {4}x{5} hr={6}" -f $dr.L, $dr.T, $dr.R, $dr.B, ($dr.R - $dr.L), ($dr.B - $dr.T), $hr
    $styleHex = "0x{0:X}" -f $style
    $exHex = "0x{0:X}" -f $exstyle
    $null = $list.Add([pscustomobject]@{
      Hwnd = $h
      Vis = [WinEnum]::IsWindowVisible($h)
      Class = $c.ToString()
      Title = $t.ToString()
      GetRect = $getRect
      DwmRect = $dwmRect
      Style = $styleHex
      ExStyle = $exHex
    })
  }
  return $true
}
[WinEnum]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$list | Format-Table -AutoSize | Out-String -Width 240
