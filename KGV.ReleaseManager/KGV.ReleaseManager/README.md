# KGV.ReleaseManager

Kleines WPF-Tool für den KGV-Release-Ablauf.

## Funktionen

- Auswahl: **Nur Windows**, **Nur Android**, **Beide**
- Versionen aus `KGV.Wpf/KGV.Wpf.csproj` und `KGV.Maui/KGV.Maui.csproj` laden
- `Major` und `Minor` manuell
- `Patch` wird automatisch auf die nächste Version vorgeschlagen
- Android-Buildnummer wird automatisch auf die nächste Zahl vorgeschlagen
- aktualisiert die Projektdateien
- startet danach die vorhandenen Batch-Dateien
  - `Setup.exe.bat`
  - `release_android.bat`
- committed danach optional das komplette Hauptprojekt `D:\Programmieren\KGV`

## Einbau

1. Projektordner `KGV.ReleaseManager` in dein Hauptrepo kopieren.
2. Das Projekt zur Solution hinzufügen.
3. Starten, Pfade prüfen, Release wählen, Version kontrollieren, `Release starten`.

## Erwarteter Aufbau

- `KGV.Wpf/KGV.Wpf.csproj`
- `KGV.Maui/KGV.Maui.csproj`
- `Setup.exe.bat`
- `release_android.bat`

## Committext

- nur WPF: `Release WPF x.y.z`
- nur Android: `Release Android x.y.z`
- beide: `Release WPF x.y.z + Android x.y.z`
