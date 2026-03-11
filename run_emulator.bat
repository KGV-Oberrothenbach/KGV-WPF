@echo off
echo ===============================
echo KGV Android Emulator Deploy
echo ===============================

:: === Konfiguration ===
set PACKAGE=de.kgv.oberrothenbach
set PROJECT=.\KGV.Maui\KGV.Maui.csproj
set EMULATOR_EXE=C:\Users\Braen\AppData\Local\Android\Sdk\emulator\emulator.exe

echo.
echo [1/6] Verfügbare Emulatoren auflisten...

setlocal enabledelayedexpansion
set count=0

for /f "delims=" %%i in ('"%EMULATOR_EXE%" -list-avds') do (
    set /a count+=1
    set EMULATOR!count!=%%i
    echo !count! - %%i
)

if %count%==0 (
    echo Kein Emulator gefunden!
    pause
    exit
)

:choose
set /p choice="Bitte Nummer des Emulators eingeben: "
if %choice% lss 1 if %choice% gtr %count% (
    echo Ungueltige Auswahl!
    goto choose
)

set EMULATOR=!EMULATOR%choice%!
echo Ausgewaehlter Emulator: %EMULATOR%

echo.
echo [2/6] Emulator starten (falls nicht aktiv)...

adb devices | find "emulator" >nul
if %errorlevel% neq 0 (
    start "" "%EMULATOR_EXE%" -avd %EMULATOR%
)

echo.
echo [3/6] Warten bis Emulator bereit ist...
adb wait-for-device

:waitboot
adb shell getprop sys.boot_completed | find "1" >nul
if %errorlevel% neq 0 (
    timeout /t 2 >nul
    goto waitboot
)

echo.
echo [4/6] Alte Version entfernen...
adb uninstall %PACKAGE% >nul 2>&1

echo.
echo [5/6] Projekt bauen und installieren...
dotnet build %PROJECT% -c Debug -f net9.0-android -t:Install --no-restore -v:q

echo.
echo [6/6] App starten...
adb shell monkey -p %PACKAGE% -c android.intent.category.LAUNCHER 1 >nul

echo.
echo Fertig
pause