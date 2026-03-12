@echo off
setlocal

set "PROJECT=D:\Programmieren\KGV\KGV.ReleaseManager\KGV.ReleaseManager\KGV.ReleaseManager.csproj"
set "OUTDIR=D:\Programmieren\KGV-Publish\KGV.ReleaseManager"

if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"
mkdir "%OUTDIR%"

dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true -o "%OUTDIR%" ^
  /p:PublishSingleFile=true ^
  /p:EnableCompressionInSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:PublishTrimmed=false ^
  /p:DebugType=none ^
  /p:DebugSymbols=false

exit /b %ERRORLEVEL%
