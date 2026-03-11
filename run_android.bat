@echo off
echo ===============================
echo KGV Android Build & Deploy
echo ===============================

echo.
echo [1/5] Alte Version entfernen...
adb uninstall de.kgv.oberrothenbach >nul 2>&1

echo.
echo [2/5] Projekt bauen...
dotnet build .\KGV.Maui\KGV.Maui.csproj -c Debug -f net9.0-android -t:Install --no-restore -v:q

echo.
echo [3/5] App starten...
adb shell monkey -p de.kgv.oberrothenbach -c android.intent.category.LAUNCHER 1 >nul

echo.
echo [4/5] Logcat starten...
echo Druecke STRG+C zum Beenden
echo ===============================
adb logcat

echo.
echo Fertig
pause
