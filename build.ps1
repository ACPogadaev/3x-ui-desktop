$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if (-not $scriptDir) { $scriptDir = Get-Location }

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    Write-Error "csc.exe not found at $csc"
    exit 1
}

Write-Host "Building 3X-UI Desktop..." -ForegroundColor Cyan

& $csc /target:winexe /codepage:65001 /utf8output /optimize+ `
    /out:"$scriptDir\3X-UI-Desktop.exe" `
    /win32icon:"$scriptDir\app.ico" `
    /win32manifest:"$scriptDir\app.manifest" `
    /r:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Web.Extensions.dll,"$scriptDir\Microsoft.Web.WebView2.WinForms.dll","$scriptDir\Microsoft.Web.WebView2.Core.dll" `
    "$scriptDir\Models.cs" `
    "$scriptDir\Win32Helper.cs" `
    "$scriptDir\SystemMonitor.cs" `
    "$scriptDir\ServerDialogs.cs" `
    "$scriptDir\SettingsDialog.cs" `
    "$scriptDir\MainForm.cs" `
    "$scriptDir\Program.cs"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build SUCCESS: $scriptDir\3X-UI-Desktop.exe" -ForegroundColor Green
} else {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}
