@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul

set "WAIT_AT_END=1"
if /I "%~1"=="NOPAUSE" set "WAIT_AT_END=0"

rem ============================================================
rem KGV - Android (MAUI) Release
rem
rem Lokal:
rem   D:\Programmieren\KGV-Publish\android\<version>\KGV-android-v<version>.apk
rem   D:\Programmieren\KGV-Publish\android\<version>\version.json
rem
rem Git-Ordner:
rem   D:\Programmieren\KGV-GitHub\android\KGV-android-v<version>.apk
rem   D:\Programmieren\KGV-GitHub\android\version.json
rem
rem Am Ende:
rem   git add .
rem   git commit -m "Android <version> veroeffentlicht"
rem   git push --set-upstream origin main
rem ============================================================

set "ROOT=%~dp0"
set "PROJECT=%ROOT%KGV.Maui\KGV.Maui.csproj"

set "BASE_URL=https://kgv-oberrothenbach.github.io/KGV-WPF"
set "GIT_REMOTE_URL=https://KGV-Oberrothenbach@github.com/KGV-Oberrothenbach/KGV-WPF.git"
set "GIT_CREDENTIAL_USERNAME=KGV-Oberrothenbach"
set "GIT_USER_NAME=KGV-Oberrothenbach"
set "GIT_USER_EMAIL="

set "LOCAL_PUBLISH_ROOT=D:\Programmieren\KGV-Publish\android"
set "GIT_ROOT=D:\Programmieren\KGV-GitHub"
set "GIT_ANDROID_DIR=%GIT_ROOT%\android"
set "KEEP_COUNT=3"

rem Optional: Signing
rem set "KEYSTORE_PATH=C:\Pfad\zu\kgv-release.keystore"
rem set "KEYSTORE_ALIAS=kgv"
rem set "KEYSTORE_PASS=..."
rem set "KEY_PASS=..."
set "KEYSTORE_PATH="
set "KEYSTORE_ALIAS="
set "KEYSTORE_PASS="
set "KEY_PASS="

echo ===============================================
echo KGV Android Release (APK)
echo ===============================================
echo.

if not exist "%PROJECT%" (
    echo FEHLER: Projektdatei nicht gefunden:
    echo   "%PROJECT%"
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

if not exist "%LOCAL_PUBLISH_ROOT%" mkdir "%LOCAL_PUBLISH_ROOT%"
if not exist "%GIT_ROOT%" (
    echo FEHLER: Git-Ordner nicht gefunden:
    echo   "%GIT_ROOT%"
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)
if not exist "%GIT_ANDROID_DIR%" mkdir "%GIT_ANDROID_DIR%"

echo [0/7] Git-Ordner synchronisieren...
pushd "%GIT_ROOT%"

git remote set-url origin "%GIT_REMOTE_URL%"
if errorlevel 1 (
    echo FEHLER: git remote set-url origin fehlgeschlagen.
    popd
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

git config --local credential.username "%GIT_CREDENTIAL_USERNAME%"
git config --local user.name "%GIT_USER_NAME%"
if not "%GIT_USER_EMAIL%"=="" git config --local user.email "%GIT_USER_EMAIL%"
git config --local pull.rebase true

set "GIT_STATUS_FILE=%TEMP%\kgv_git_status_android_%RANDOM%.txt"
git status --porcelain > "%GIT_STATUS_FILE%" 2>nul
for %%A in ("%GIT_STATUS_FILE%") do set "GIT_STATUS_SIZE=%%~zA"
if not defined GIT_STATUS_SIZE set "GIT_STATUS_SIZE=0"
del /q "%GIT_STATUS_FILE%" 2>nul

if not "%GIT_STATUS_SIZE%"=="0" (
    echo FEHLER: Der Git-Ordner hat bereits lokale Aenderungen.
    echo Bitte zuerst im Ordner "%GIT_ROOT%" committen, pushen oder bereinigen.
    git status --short
    popd
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

git pull --rebase origin main
if errorlevel 1 (
    echo FEHLER: git pull --rebase origin main fehlgeschlagen.
    popd
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

popd

set "APP_VERSION="
set "APP_BUILD="

for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "(Select-Xml -Path '%PROJECT%' -XPath '//ApplicationDisplayVersion').Node.InnerText"`) do set "APP_VERSION=%%i"
for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "(Select-Xml -Path '%PROJECT%' -XPath '//ApplicationVersion').Node.InnerText"`) do set "APP_BUILD=%%i"

if not defined APP_VERSION (
    echo FEHLER: ApplicationDisplayVersion konnte nicht gelesen werden.
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)
if not defined APP_BUILD (
    echo FEHLER: ApplicationVersion konnte nicht gelesen werden.
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "$v='%APP_VERSION%'; if($v -match '^\d+\.\d+\.\d+\.0$'){ $v.Substring(0,$v.Length-2) } else { $v }"`) do set "APP_VERSION=%%v"

if "%APP_VERSION%"=="" (
    echo FEHLER: Version ist leer.
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

set "BUILD_OUTPUT=%ROOT%artifacts\android\publish\%APP_VERSION%"
set "VERSION_DIR=%LOCAL_PUBLISH_ROOT%\%APP_VERSION%"
set "APK_NAME=KGV-android-v%APP_VERSION%.apk"
set "LOCAL_APK_PATH=%VERSION_DIR%\%APK_NAME%"
set "LOCAL_JSON_PATH=%VERSION_DIR%\version.json"
set "GIT_APK_PATH=%GIT_ANDROID_DIR%\%APK_NAME%"
set "GIT_JSON_PATH=%GIT_ANDROID_DIR%\version.json"
set "DOWNLOAD_URL=%BASE_URL%/android/%APK_NAME%"

echo Version: %APP_VERSION%  Build: %APP_BUILD%
echo Projekt: %PROJECT%
echo Lokaler Zielordner: %VERSION_DIR%
echo Git-Zielordner:     %GIT_ANDROID_DIR%
echo.

echo [1/7] dotnet clean...
dotnet clean "%PROJECT%" -c Release
if errorlevel 1 (
    echo FEHLER: dotnet clean fehlgeschlagen.
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

echo.
echo [2/7] dotnet restore...
dotnet restore "%PROJECT%"
if errorlevel 1 (
    echo FEHLER: dotnet restore fehlgeschlagen.
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

echo.
echo [3/7] dotnet publish...
if exist "%BUILD_OUTPUT%" rmdir /s /q "%BUILD_OUTPUT%" >nul 2>&1
mkdir "%BUILD_OUTPUT%" >nul 2>&1

if not "%KEYSTORE_PATH%"=="" (
    if "%KEYSTORE_ALIAS%"=="" (
        echo FEHLER: KEYSTORE_ALIAS fehlt.
        if "%WAIT_AT_END%"=="1" pause
        exit /b 1
    )
    if "%KEYSTORE_PASS%"=="" (
        echo FEHLER: KEYSTORE_PASS fehlt.
        if "%WAIT_AT_END%"=="1" pause
        exit /b 1
    )
    if "%KEY_PASS%"=="" (
        echo FEHLER: KEY_PASS fehlt.
        if "%WAIT_AT_END%"=="1" pause
        exit /b 1
    )

    echo Signing: AKTIV

    dotnet publish "%PROJECT%" -c Release -f net9.0-android -p:AndroidPackageFormat=apk -o "%BUILD_OUTPUT%" ^
      -p:AndroidKeyStore=true ^
      -p:AndroidSigningKeyStore="%KEYSTORE_PATH%" ^
      -p:AndroidSigningKeyAlias="%KEYSTORE_ALIAS%" ^
      -p:AndroidSigningStorePass="%KEYSTORE_PASS%" ^
      -p:AndroidSigningKeyPass="%KEY_PASS%"
) else (
    echo Signing: STANDARD
    echo Hinweis: Fuer echte Verteilung sollte ein Release-Keystore gesetzt werden.

    dotnet publish "%PROJECT%" -c Release -f net9.0-android -p:AndroidPackageFormat=apk -o "%BUILD_OUTPUT%"
)

if errorlevel 1 (
    echo FEHLER: dotnet publish fehlgeschlagen.
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

echo.
echo [4/7] APK finden und verteilen...
set "BUILT_APK="

for /r "%BUILD_OUTPUT%" %%f in (*-Signed.apk) do (
    if not defined BUILT_APK set "BUILT_APK=%%f"
)
if not defined BUILT_APK (
    for /r "%BUILD_OUTPUT%" %%f in (*Signed.apk) do (
        if not defined BUILT_APK set "BUILT_APK=%%f"
    )
)
if not defined BUILT_APK (
    for /r "%BUILD_OUTPUT%" %%f in (*.apk) do (
        if not defined BUILT_APK set "BUILT_APK=%%f"
    )
)

if not defined BUILT_APK (
    echo FEHLER: Keine APK gefunden:
    echo   "%BUILD_OUTPUT%"
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

echo Gefundene APK:
echo   "%BUILT_APK%"

if exist "%VERSION_DIR%" rmdir /s /q "%VERSION_DIR%" >nul 2>&1
mkdir "%VERSION_DIR%" >nul 2>&1

copy /y "%BUILT_APK%" "%LOCAL_APK_PATH%" >nul
if errorlevel 1 (
    echo FEHLER: Konnte lokale APK nicht schreiben.
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

copy /y "%BUILT_APK%" "%GIT_APK_PATH%" >nul
if errorlevel 1 (
    echo FEHLER: Konnte Git-APK nicht schreiben.
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

echo.
echo [5/7] version.json schreiben...
powershell -NoProfile -Command ^
  "$o = [ordered]@{ platform='android'; version='%APP_VERSION%'; build=[int]%APP_BUILD%; fileName='%APK_NAME%'; downloadUrl='%DOWNLOAD_URL%'; mandatory=$false; notes='Neue Android-Version' };" ^
  "$json = ConvertTo-Json -InputObject $o -Depth 5;" ^
  "Set-Content -Path '%LOCAL_JSON_PATH%' -Value $json -Encoding utf8;" ^
  "Set-Content -Path '%GIT_JSON_PATH%' -Value $json -Encoding utf8"
if errorlevel 1 (
    echo FEHLER: version.json konnte nicht geschrieben werden.
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

echo.
echo [6/7] Alte Android-Versionen bereinigen...

powershell -NoProfile -Command ^
  "$keep=%KEEP_COUNT%;" ^
  "$root='%LOCAL_PUBLISH_ROOT%';" ^
  "if(Test-Path $root){" ^
  "  Get-ChildItem -LiteralPath $root -Directory |" ^
  "    Where-Object { $_.Name -match '^\d+(\.\d+){1,3}$' } |" ^
  "    Sort-Object { [version]$_.Name } -Descending |" ^
  "    Select-Object -Skip $keep |" ^
  "    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue" ^
  "}"
if errorlevel 1 (
    echo FEHLER: Konnte lokale Android-Versionen nicht bereinigen.
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

powershell -NoProfile -Command ^
  "$keep=%KEEP_COUNT%;" ^
  "$root='%GIT_ANDROID_DIR%';" ^
  "if(Test-Path $root){" ^
  "  Get-ChildItem -LiteralPath $root -File -Filter 'KGV-android-v*.apk' |" ^
  "    Sort-Object { if($_.BaseName -match '^KGV-android-v(?<v>\d+(?:\.\d+){1,3})$'){ [version]$Matches['v'] } else { [version]'0.0.0.0' } } -Descending |" ^
  "    Select-Object -Skip $keep |" ^
  "    Remove-Item -Force -ErrorAction SilentlyContinue" ^
  "}"
if errorlevel 1 (
    echo FEHLER: Konnte Git-Android-Versionen nicht bereinigen.
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

echo.
echo [7/7] Git-Aenderungen pruefen und hochladen...
pushd "%GIT_ROOT%"

git status --short

git add .
if errorlevel 1 (
    echo FEHLER: git add fehlgeschlagen.
    popd
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

git diff --cached --quiet
if not errorlevel 1 (
    echo Keine Git-Aenderungen zum Committen gefunden.
    popd
    echo.
    echo ===============================================
    echo Fertig
    echo ===============================================
    echo Lokale APK:    "%LOCAL_APK_PATH%"
    echo Lokale JSON:   "%LOCAL_JSON_PATH%"
    echo Git APK:       "%GIT_APK_PATH%"
    echo Git JSON:      "%GIT_JSON_PATH%"
    echo.
    if "%WAIT_AT_END%"=="1" pause
    endlocal
    exit /b 0
)

git commit -m "Android %APP_VERSION% veroeffentlicht"
if errorlevel 1 (
    echo FEHLER: git commit fehlgeschlagen.
    popd
    if "%WAIT_AT_END%"=="1" pause
    exit /b 1
)

git push --set-upstream origin main
if errorlevel 1 (
    echo Erster git push fehlgeschlagen. Versuche git pull --rebase origin main und erneuten Push...
    git pull --rebase origin main
    if errorlevel 1 (
        echo FEHLER: git pull --rebase origin main nach Push-Fehler fehlgeschlagen.
        popd
        if "%WAIT_AT_END%"=="1" pause
        exit /b 1
    )

    git push --set-upstream origin main
    if errorlevel 1 (
        echo FEHLER: git push fehlgeschlagen.
        popd
        if "%WAIT_AT_END%"=="1" pause
        exit /b 1
    )
)

popd

echo Git-Upload erfolgreich.
echo.
echo ===============================================
echo Fertig
echo ===============================================
echo Lokale APK:    "%LOCAL_APK_PATH%"
echo Lokale JSON:   "%LOCAL_JSON_PATH%"
echo Git APK:       "%GIT_APK_PATH%"
echo Git JSON:      "%GIT_JSON_PATH%"
echo.
if "%WAIT_AT_END%"=="1" pause
endlocal
exit /b 0