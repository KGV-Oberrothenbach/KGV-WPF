# Copilot Instructions

## Project Guidelines
- Im Projekt „KGV“ Änderungen klein/nachvollziehbar; keine neuen Features bis WPF stabil; keine async void außer UI-Events; keine UI-Logik in Services; pro Schritt (1) betroffene Dateien, (2) konkrete Änderungen, (3) Build/Run-Check; bei Code immer komplette Datei liefern; Phase 0: auf Branch „baseline-wpf-stabilisieren“ arbeiten, ggf. stash.
- In Zusammenfassungen keine vollständigen Dateien ausgeben; nur geänderte Dateien nennen und Änderungen kurz beschreiben.
- Nach Änderungen im KGV-Projekt immer `Documentation/DEV_LOG.md` befüllen und im Ergebnis explizit erwähnen.
- In `KGV.Wpf.csproj`, immer `Version`, `AssemblyVersion`, `FileVersion` und `InformationalVersion` zusammen für jede neue Veröffentlichung erhöhen (bugfix: x.y.z + x.y.z.0 + x.y.z.0 + x.y.z-beta; feature: x.y.0 + x.y.0.0 + x.y.0.0 + x.y.0-beta; release: 1.0.0 + 1.0.0.0 + 1.0.0.0 + 1.0.0). Keine spezielle abweichende Versionierungslogik.