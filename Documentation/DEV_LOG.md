# KGV Entwicklungslog

---

## Workaround & Vorgehensweise bei Änderungen

- Änderungen nur nach genauer Rückfrage und vollständiger Ansicht der Datei vornehmen.  
- Ich möchte immer genau wissen, **wo und wie** die Änderungen erfolgen.  
- Am liebsten **komplett die Datei** erhalten, damit der neue Code sauber angepasst werden kann.  
- Wenn Unsicherheit besteht, fordere ich immer die aktuelle Datei an, bevor Änderungen vorgeschlagen werden.  

---

## 2026-02-18 – Projektstart

### Erledigt
- Neues WPF Projekt (.NET 8) erstellt
- MVVM + DI Struktur geplant und vorbereitet
- Dokumentationsstruktur angelegt

### Erkenntnisse
- Sauberer Neustart ist effizienter als Reparieren
- Klare Architektur spart Debugzeit

### Offene Aufgaben
- LoginView implementieren
- MainViewModel aufsetzen
- SupabaseService minimal implementieren

---

## 2026-02-18 – Architektur & MVVM

### Erledigt
- MVVM Ordnerstruktur erstellt
- BaseViewModel als ObservableObject vorbereitet
- NavigationService Interface erstellt
- CommunityToolkit.Mvvm eingebunden
- Microsoft.Extensions.DependencyInjection eingebunden
- MainViewModel Grundgerüst erstellt
- StartViewModel Grundgerüst erstellt

### Erkenntnisse
- Saubere Trennung von Verantwortlichkeiten reduziert Fehler
- UI-Logik muss komplett vom Datenzugriff getrennt sein

---

## 2026-02-18 – LoginView & StartViewModel

### Erledigt
- LoginView mit Email- und Passwortfeld erstellt
- Toggle-Passwort-Visibility implementiert
- StartViewModel: LoginCommand angelegt
- Passwort aus PasswordBox ausgelesen
- MessageBox-Feedback bei leerer Eingabe

### Erkenntnisse
- Binding allein reicht für Passwort nicht aus (PasswordBox ist nicht bindbar)
- Temporär über View auf PasswordBox zuzugreifen ist nötig

---

## 2026-02-18 – MainWindow & MainViewModel

### Erledigt
- MainWindow Layout erstellt mit Navigation links / Arbeitsbereich rechts
- Mitgliedersuche implementiert (WatermarkHelper statt PlaceholderText)
- Suchergebnisse als ListBox
- Untermenüs für ausgewähltes Mitglied dynamisch
- Export-Button implementiert
- MainViewModel mit ObservableCollections und Commands

### Erkenntnisse
- PlaceholderText ist in WPF nicht vorhanden → WatermarkHelper nutzen
- Commands sollten aus ViewModel angesteuert werden, Events nur temporär für UI

---

## 2026-02-18 – MemberViewModel & DTO

### Erledigt
- MemberDTO erstellt mit Stammdaten:
  - Vorname, Nachname, Strasse, Plz, Ort, Telefon, Email, Bemerkungen, WhatsappEinwilligung
- Interne/Admin-Felder:
  - AuthUserId, IstKGV, Aktiv, Role
- MemberViewModel bindet an DTO und ObservableProperties
- LoadFromDTO und SaveChanges implementiert
- PLZ-Property-Fix: Variablenname korrekt auf `Plz` geändert

### Erkenntnisse
- Non-Nullable Warnungen (CS8618) durch Initialisierung lösen oder `required` nutzen
- Alle Änderungen müssen sauber zwischen DTO und ViewModel synchronisiert werden

---

## 2026-02-18 – App.xaml.cs

### Erledigt
- DI Setup für Services implementiert
- Startup-Event sauber ersetzt, alte `Application_Startup` entfernt
- CS1061 Fehlerquelle behoben

### Erkenntnisse
- Wichtiger Workaround: Änderungen nur mit kompletter Dateiansicht vornehmen

---

## Nächste Schritte

1. SupabaseService minimal implementieren, um Login und Stammdaten zu testen  
2. Dashboard-Layout fertigstellen, Inhalte aus MainViewModel laden  
3. Member-CRUD Funktionen implementieren (Anlegen, Bearbeiten, Löschen)  
4. Rollen- und Policy-System final im ViewModel/Service einbauen  
5. Exportfunktionen fertigstellen  
6. Warnung CS8618 in App.xaml.cs überprüfen / ggf. `required` verwenden  
7. PLZ-Fix in allen ViewModels konsistent anwenden  
8. Unit Tests für ViewModels vorbereiten

---

## 2026-03-13 – Release Notes / Changelog Workflow

### Erledigt
- Zentrale Changelog-Datei und Release-Notes-Historie eingeführt (menschen- und maschinenlesbar).
- ReleaseManager um manuellen Flow erweitert: Daten aus Changelog kopieren, finalen Text einfügen und versionsbezogen speichern.
- `releases.json` wird beim Release in den GitHub-Ordner kopiert (Basis für spätere Web-Anzeige).

### Hinweise
- Altstände sind nur begrenzt rekonstruierbar; ältere Versionen bleiben daher bewusst knapp dokumentiert.

---

## 2026-03-13 – Startseite-Verwaltung: Arbeitseinsätze (Save) & Formular-UX

### Erledigt
- Persistierung für Arbeitseinsätze korrigiert: Schreibzugriff erfolgt nicht mehr über die (nicht updatable) View, sondern über ein dediziertes Write-Model auf Basistabelle; danach wird der Datensatz erneut aus der View geladen (computed/read-only Felder bleiben korrekt).
- Formular-UX in WPF-Verwaltung vereinheitlicht (Arbeitseinsatz/Termin/Bekanntmachung): Standardzustand ohne Formular, Bearbeiten nur via `Neu` bzw. Doppelklick, `Speichern`/`Abbrechen` am Formularende, Rückfrage bei ungespeicherten Änderungen.
- Analoges Muster in MAUI-Admin-Seiten umgesetzt: Formular standardmäßig ausgeblendet, Bearbeiten via `Neu` bzw. Listentap, `Speichern`/`Abbrechen` am Formularende, Rückfrage bei ungespeicherten Änderungen.

### Hinweise
- In MAUI gibt es kein echtes „Doppelklick“-Pattern; dort öffnet ein Tap auf einen Listeneintrag den Bearbeitungsmodus.

---

## 2026-03-13 – Startseite-Verwaltung: DB-Schema-Fix (Basistabellen/Views), ID-Erzeugung, Pflichtfelder

### Erledigt
- Tabellen-/View-Mapping finalisiert:
  - Writes (Insert/Update) laufen nun ausschließlich gegen die Basistabellen `arbeitseinsatz`, `termin`, `bekanntmachung`.
  - Reads bleiben über die Views `v_startseite_arbeitseinsatz`, `v_startseite_termine`, `v_startseite_bekanntmachungen`.
- ID-Handling defensiver gemacht: nach Insert/Update wird geprüft, dass eine gültige ID zurückkommt (sonst klare Fehlermeldung bzgl. Identity/Sequence/Trigger).
- Technische Pflichtfelder an DB-Lage angepasst:
  - `sichtbar_ab` wird nicht mehr als technisches Pflichtfeld validiert/mit `*` markiert.
  - `sort_order` wird in MAUI nicht mehr als technisches Pflichtfeld erzwungen (leere Eingabe -> NULL).
- `stunden_wert` wird beim Schreiben nie als NULL gesendet (DB: NOT NULL, Default 0).

### Hinweise
- In WPF bleibt `SortOrder` aktuell UI-seitig als Zahl geführt (ohne Pflicht-Stern), auch wenn die DB NULL erlaubt.

---

## 2026-03-13 – Formulare/Validierung (nullable Felder) & Android App-Icon

### Erledigt
- WPF Bekanntmachungen: `SortOrder` im Edit-Flow auf nullable umgestellt (leere Eingabe -> NULL; nur bei Eingabe int-Validierung).
- MAUI Admin-Formulare: optionale Datumsfelder (`SichtbarBis`, Arbeitseinsatz zusätzlich `AnmeldungBis`) via Toggle wirklich optional gemacht (Toggle aus -> Save schreibt NULL).
- Save-Buttons: in WPF (Commands) und MAUI (Button-State) an Mindestvalidität + tatsächliche Änderungen gekoppelt.
- Android App-Icon: `KGV.Maui/Resources/AppIcon/appicon.png` durch `Logo.png` ersetzt; ungenutzte Standard-Icon-SVGs entfernt.

### Hinweise
- ID-Erzeugung (Identity/Sequence/Trigger) ist im Repo nicht migrationsseitig belegbar; Client prüft nach Save defensiv auf eine gültige zurückgegebene ID.
- Für Icon-Wechsel auf Android sind i.d.R. Clean/Reinstall nötig (Build/Launcher-Caches).

### Betroffene Dateien (Details)
- WPF
  - `KGV.Wpf/ViewModels/BekanntmachungenVerwaltungViewModel.cs`
  - `KGV.Wpf/ViewModels/TermineVerwaltungViewModel.cs`
  - `KGV.Wpf/ViewModels/ArbeitseinsaetzeVerwaltungViewModel.cs`
- MAUI
  - `KGV.Maui/Pages/BekanntmachungenAdminPage.cs`
  - `KGV.Maui/Pages/TermineAdminPage.cs`
  - `KGV.Maui/Pages/ArbeitseinsaetzeAdminPage.cs`
- Android Icon
  - `KGV.Maui/Resources/AppIcon/appicon.png` (Quelle: `Logo.png`)
  - `KGV.Maui/Resources/AppIcon/appicon.svg` (entfernt)
  - `KGV.Maui/Resources/AppIcon/appiconfg.svg` (entfernt)

### Technische Notizen
- MAUI: optionale Datumsfelder werden über Switch gesteuert (Toggle aus -> Feld wird als `null` gespeichert, DatePicker wird verborgen/deaktiviert).
- WPF: `SortOrder` wird als Text gehalten und erst beim Speichern/CanExecute validiert (leer -> `null`).

---

## 2026-03-13 – ReleaseManager: CHANGELOG als Quelle (statt DEV_LOG)

### Erledigt
- Rollen sauber getrennt: `DEV_LOG.md` bleibt technische Arbeitsdoku; `CHANGELOG.md` ist release-tauglich für den ReleaseManager.
- `CHANGELOG.md` in eine stabile Grundstruktur überführt (`# Changelog`, `## [Unreleased]`, Kategorien).
- ReleaseManager erweitert: eigener CHANGELOG-Block kann aus `CHANGELOG.md` geladen und dorthin zurückgeschrieben werden (Ziel: Feld ist nicht mehr leer/unbrauchbar).

### Hinweise
- ReleaseManager greift weiterhin ausschließlich auf `Documentation/CHANGELOG.md` zu; `DEV_LOG.md` wird in diesem Workflow nicht geschrieben.
