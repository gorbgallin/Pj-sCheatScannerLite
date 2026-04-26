@echo off
setlocal

title Pj's Cheat Scanner - Lite
cd /d "%~dp0"

if not exist "PjCheatScannerLite.exe" (
    echo [ERROR] PjCheatScannerLite.exe not found.
    echo Make sure you extracted ALL files from the zip.
    goto :PauseAndExit
)

REM Check for exact .NET 8.0 Desktop Runtime
where dotnet >nul 2>nul
if errorlevel 1 goto :MissingDotNet

dotnet --list-runtimes 2>nul | findstr /I /C:"Microsoft.WindowsDesktop.App 8.0" >nul
if errorlevel 1 goto :MissingDotNet

REM Exact runtime found — start immediately
goto :RunScanner

:MissingDotNet
echo.
echo ============================================
echo   .NET 8.0 Desktop Runtime NOT FOUND
echo ============================================
echo.
echo This tool requires the .NET 8.0 Desktop Runtime.
echo.
set /p CHOICE="Would you like to install it now? [Y/n]: "
if /I "%CHOICE%"=="n" goto :DeclinedInstall
if /I "%CHOICE%"=="no" goto :DeclinedInstall

REM User wants to install
echo.
echo Checking for Windows Package Manager (winget)...
where winget >nul 2>nul
if errorlevel 1 goto :TryDirectDownload

echo Found winget. Installing .NET 8 Desktop Runtime...
echo This may take a few minutes... Please wait.
winget install Microsoft.DotNet.DesktopRuntime.8 --silent --accept-source-agreements --accept-package-agreements
if errorlevel 1 goto :TryDirectDownload

echo.
echo .NET 8 installed via winget!
echo You may need to restart this tool if it still fails.
goto :RunScanner

:TryDirectDownload
echo.
echo Downloading .NET 8 Desktop Runtime installer...
set "INSTALLER=%TEMP%\dotnet8-runtime-installer.exe"
powershell -Command "Invoke-WebRequest -Uri 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe' -OutFile '%INSTALLER%' -UseBasicParsing"
if not exist "%INSTALLER%" goto :DownloadFailed

echo Running installer silently...
"%INSTALLER%" /install /quiet /norestart
del /f "%INSTALLER%" >nul 2>nul

echo.
echo Installer finished. You may need to restart your PC.
goto :RunScanner

:DownloadFailed
echo.
echo [ERROR] Failed to download the installer automatically.
echo Please install manually from: https://dotnet.microsoft.com/download/dotnet/8.0
goto :PauseAndExit

:DeclinedInstall
echo.
echo Install cancelled. You can download it manually from:
echo https://dotnet.microsoft.com/download/dotnet/8.0
goto :PauseAndExit

:RunScanner
echo.
echo ============================================
echo   Starting Pj's Cheat Scanner - Lite
echo ============================================
echo.
echo TIP: For full memory access, close this window
echo and right-click this file -^> "Run as administrator".
echo.

PjCheatScannerLite.exe
if errorlevel 1 (
    echo.
    echo [Scanner exited with error %errorlevel%]
)
goto :PauseAndExit

:PauseAndExit
echo.
pause
endlocal
exit /b
