@echo off
setlocal EnableExtensions
chcp 65001 >nul

set "ROOT=%~dp0"
set "WPF_BATCH=%ROOT%Setup.exe.bat"
set "ANDROID_BATCH=%ROOT%release_android.bat"

echo ===============================================
echo KGV Gesamt-Release WPF + Android
echo ===============================================
echo.
echo Projektordner: %ROOT%
echo.

if not exist "%WPF_BATCH%" (
    echo FEHLER: Setup.exe.bat nicht gefunden:
    echo   "%WPF_BATCH%"
    pause
    exit /b 1
)

if not exist "%ANDROID_BATCH%" (
    echo FEHLER: release_android.bat nicht gefunden:
    echo   "%ANDROID_BATCH%"
    pause
    exit /b 1
)

echo [1/2] Starte WPF-Release...
call "%WPF_BATCH%" NOPAUSE
if errorlevel 1 (
    echo.
    echo FEHLER: WPF-Release ist fehlgeschlagen.
    pause
    exit /b 1
)

echo.
echo [2/2] Starte Android-Release...
call "%ANDROID_BATCH%" NOPAUSE
if errorlevel 1 (
    echo.
    echo FEHLER: Android-Release ist fehlgeschlagen.
    pause
    exit /b 1
)

echo.
echo ===============================================
echo Beide Releases wurden abgeschlossen.
echo ===============================================
echo.
pause
endlocal
exit /b 0