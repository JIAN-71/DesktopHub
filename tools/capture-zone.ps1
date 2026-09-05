param([string]$outPath, [int]$x = 1000, [int]$y = 0, [int]$w = 800, [int]$h = 220, [int]$ProcId = 0)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class DpiHelper {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[void][DpiHelper]::SetProcessDPIAware()

$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size $w, $h))
$g.Dispose()
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "saved $outPath ($x,$y) ${w}x${h} physical"

# DPI-aware 下重读胶囊窗口物理矩形 + 系统 DPI
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WinProbe {
  public delegate bool EnumProc(IntPtr hwnd, IntPtr lparam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lparam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT r, int size);
}
"@
$targetPid2 = [uint32]$ProcId
$cb = [WinProbe+EnumProc]{
  param($hh, $ll)
  $wpid = [uint32]0
  [WinProbe]::GetWindowThreadProcessId($hh, [ref]$wpid) | Out-Null
  if ($wpid -eq $targetPid2) {
    $r = New-Object WinProbe+RECT
    [WinProbe]::GetWindowRect($hh, [ref]$r) | Out-Null
    $d = New-Object WinProbe+RECT
    [WinProbe]::DwmGetWindowAttribute($hh, 9, [ref]$d, 16) | Out-Null
    Write-Output ("PILL aware GetWindowRect=({0},{1})-({2},{3}) {4}x{5}  DwmBounds=({6},{7})-({8},{9}) {10}x{11}  WinDpi={12} SysDpi={13}" -f `
      $r.L, $r.T, $r.R, $r.B, ($r.R - $r.L), ($r.B - $r.T), $d.L, $d.T, $d.R, $d.B, ($d.R - $d.L), ($d.B - $d.T), [WinProbe]::GetDpiForWindow($hh), [WinProbe]::GetDpiForSystem())
  }
  return $true
}
[WinProbe]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
