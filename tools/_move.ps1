Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class MM {
  [StructLayout(LayoutKind.Sequential)]
  public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)]
  public struct INPUT { public uint type; public MOUSEINPUT mi; }
  [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
  public const uint MOUSEEVENTF_MOVE = 0x0001;
  public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
}
"@
function MoveTo([int]$x, [int]$y) {
  $ax = [int]($x * 65535 / 2560); $ay = [int]($y * 65535 / 1600)
  $i = New-Object "MM+INPUT"
  $i.type = 0; $i.mi.dx = $ax; $i.mi.dy = $ay; $i.mi.dwFlags = 0x8001
  [MM]::SendInput(1, @($i), [System.Runtime.InteropServices.Marshal]::SizeOf([type][MM+INPUT])) | Out-Null
}
MoveTo 1900 1000
Write-Output "moved to 1900,1000"
