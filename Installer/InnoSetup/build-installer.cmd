@echo off
setlocal

REM Build script for KGV.Wpf + Inno Setup
REM Prerequisites:
REM - .NET SDK installed
REM - Inno Setup installed (ISCC.exe available)

set "SolutionRoot=%~dp0..\.."
set "Project=%SolutionRoot%\KGV.Wpf\KGV.Wpf.csproj"

REM Fixed publish folder (can be used by the app updater and by Inno Setup)
set "PublishDir=D:\Programmieren\KGV-Publish\AppFiles\Current"

REM Where the compiled installer should land (stable filename for GitHub Pages)
set "InstallerOutDir=D:\Programmieren\KGV-Publish\Installers\Current"

REM Adjust as needed: win-x64 gives you a stable apphost EXE (KGV.Wpf.exe)
dotnet publish "%Project%" -c Release -r win-x64 --self-contained false -o "%PublishDir%" || exit /b 1

REM Compile installer
set "ISCC="

REM 1) Prefer PATH (if user added Inno Setup to PATH)
for /f "delims=" %%I in ('where ISCC.exe 2^>nul') do (
	set "ISCC=%%I"
	goto :iscc_found
)

REM 2) Common install paths
if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"

REM 3) Per-user install (winget often installs here)
if not defined ISCC if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"

:iscc_found
if not defined ISCC (
	echo.
	echo ERROR: ISCC.exe not found. Please install Inno Setup 6.
	echo        winget install --id JRSoftware.InnoSetup -e
	echo.
	exit /b 2
)

"%ISCC%" /DPublishDir="%PublishDir%" /O"%InstallerOutDir%" "%SolutionRoot%\Installer\InnoSetup\KGV.Wpf.iss" || exit /b 1

echo.
echo OK - Installer created in: %InstallerOutDir%
echo.
endlocal
