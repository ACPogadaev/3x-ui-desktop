@echo off
setlocal
cd /d "%~dp0"
chcp 65001 >nul
echo [3X-UI Desktop] Building application...

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] Compiler csc.exe not found at %CSC%
    pause
    exit /b 1
)

"%CSC%" /target:winexe /codepage:65001 /utf8output /optimize+ /out:"3X-UI-Desktop.exe" /win32icon:"app.ico" /win32manifest:"app.manifest" /r:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Web.Extensions.dll,"Microsoft.Web.WebView2.WinForms.dll","Microsoft.Web.WebView2.Core.dll" Models.cs Win32Helper.cs SystemMonitor.cs ServerDialogs.cs SettingsDialog.cs MainForm.cs Program.cs

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] 3X-UI-Desktop.exe built successfully!
) else (
    echo.
    echo [ERROR] Build failed.
)

if not "%1"=="nopause" pause
