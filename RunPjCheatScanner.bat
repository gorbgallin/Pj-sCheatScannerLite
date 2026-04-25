@echo off
REM RunPjCheatScanner.bat - Lightweight bootstrapper that checks for .NET 6+ before launching Pj's Cheat Scanner
REM If .NET is missing, offers to open the download page automatically

REM Check if dotnet command is available
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ============================================
    echo   .NET 6.0+ Runtime NOT FOUND
    echo ============================================
    echo.
    echo This tool requires the .NET 6.0 Desktop Runtime.
    echo.
    set /p openPage="Open download page now? (Y/N): "
    if /I "%openPage%"=="Y" (
        start https://dotnet.microsoft.com/en-us/download/dotnet/6.0
    )
    echo.
    echo After installing .NET 6.0, re-run this tool.
    pause
    exit /b 1
)

REM .NET found - run the app
echo Starting Pj's Cheat Scanner...
dotnet "%~dp0PjCheatScannerLite.dll"
pause

