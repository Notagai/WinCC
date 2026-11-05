param([string]$OutputDirectory = "artifacts/windows-evidence")
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Run this on the Windows 11 desktop used for testing.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$os = Get-CimInstance Win32_OperatingSystem
$evidence = [ordered]@{
    Timestamp = (Get-Date).ToUniversalTime().ToString('o')
    Commit = (git rev-parse HEAD)
    WorkingTreeChanges = @(git status --short)
    OperatingSystem = $os.Caption
    Build = $os.BuildNumber
    Architecture = $os.OSArchitecture
    Gpu = @(Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion)
    WallpaperEngineRunning = [bool](Get-Process wallpaper32, wallpaper64 -ErrorAction SilentlyContinue)
    ExplorerRunning = [bool](Get-Process explorer -ErrorAction SilentlyContinue)
    NativeTestResult = 'NOT RUN: inventory does not execute the acceptance matrix'
}
$evidence | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'inventory.json')
Copy-Item docs/TEST-MATRIX.md (Join-Path $OutputDirectory 'TEST-MATRIX.md')
Write-Output "Inventory saved to $OutputDirectory. Record monitor placement/scaling and actual per-test observations separately. No tests were marked passed."
