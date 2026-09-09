param([long]$hwnd, [string]$dir, [int]$dirDown = 1)
# 向胶囊窗 PostMessage WM_MOUSEWHEEL 触发前置区轮换,并在动画期间连续 PrintWindow 抓帧
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class SW2 {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wp, IntPtr lp);
  public struct RECT { public int L, T, R, B; }
}
'@
[void][SW2]::SetProcessDPIAware()

function Capture([string]$path) {
  $r = New-Object SW2+RECT
  [SW2]::GetWindowRect([IntPtr]$hwnd, [ref]$r) | Out-Null
  $w = $r.R - $r.L; $h = $r.B - $r.T
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $hdc = $g.GetHdc()
  [SW2]::PrintWindow([IntPtr]$hwnd, $hdc, 2) | Out-Null
  $g.ReleaseHdc($hdc); $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Output ("{0} ({1}x{2})" -f $path, $w, $h)
}

if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
$delta = $(if ($dirDown -eq 1) { -120 } else { 120 })   # dirDown=1 下滚, dirDown=0 上滚
$wp = [IntPtr]((($delta -band 0xFFFF) -shl 16))
[SW2]::PostMessage([IntPtr]$hwnd, 0x020A, $wp, [IntPtr]::Zero) | Out-Null
for ($i = 0; $i -lt 6; $i++) {
  Capture (Join-Path $dir ("swap-{0:D2}.png" -f $i))
  Start-Sleep -Milliseconds 55
}
Start-Sleep -Milliseconds 300
Capture (Join-Path $dir "swap-final.png")
