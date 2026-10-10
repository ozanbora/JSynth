# Saves a picture of the first window whose title matches, brought to the front first.
# A Windows build's window belongs to JSynth.Desktop; a WSL build's to msrdc, its title ending "(Ubuntu)".
param(
    [Parameter(Mandatory)] [string] $Out,
    [string] $Title = '*JSynth*'
)

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class JsWin {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
'@

$window = Get-Process | Where-Object { $_.MainWindowTitle -like $Title } | Select-Object -First 1
if (-not $window) { throw "No window titled like '$Title'." }
if ($window.MainWindowTitle -like '`[WARN:COPY MODE`]*') {
    throw "WSLg is in copy mode and draws nothing: run 'wsl --shutdown' (stops every distro) and start again."
}

[JsWin]::SetForegroundWindow($window.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 800
$r = New-Object JsWin+Rect
[JsWin]::GetWindowRect($window.MainWindowHandle, [ref]$r) | Out-Null

$bitmap = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($r.Left, $r.Top, 0, 0, $bitmap.Size)
$bitmap.Save($Out)
"$($window.MainWindowTitle) -> $Out"
