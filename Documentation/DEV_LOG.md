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

---

## 2026-03-13 – Terminzeiten (Supabase time), Bekanntmachungen-Formatierung, Impressum, Wartungsverträge, Arbeitsstunden

### Erledigt
- Termin-Speichern abgesichert: Start/Ende werden vor DB-Write auf gültiges `HH:mm` normalisiert; leere/ungültige Werte werden als `null` gesendet (verhindert Postgres-Fehler `invalid input syntax for type time`).
- Termin-UI verbessert:
  - WPF: Start/Ende als editierbare `ComboBox` mit 30-Minuten-Auswahl + freier Eingabe.
  - MAUI: Start/Ende via `Picker` (30-Minuten) + `Entry` (freie Eingabe), Normalisierung beim Verlassen des Feldes + Validierung beim Speichern.
  - Defaults: Beginn `10:00`, Ende `13:00`.
- Bekanntmachungen: Formatierungs-UI (Schriftgröße/Fett/Kursiv) wirkt nun sichtbar im Editor (WPF+MAUI).
- Impressum:
  - Anzeige nur noch Handynummern (Telefon entfernt).
  - Feld "Verantwortlich" ist frei editierbar; Speicherung aktuell lokal in User-Settings.
  - Nach erfolgreichem Speichern wird der Bearbeiten-Modus sicher beendet.
- Wartungsverträge: erster nutzbarer Verwaltungsstand (Anlegen/Bearbeiten) in WPF+MAUI ergänzt.
- Arbeitsstunden/Pflichtstunden:
  - Fehlbetrag fachlich korrigiert: `max(0, offen * EuroProFehlstunde)` und nie negativ.
  - Anzeige als Euro-Text (z.B. `250,-€`).
  - Vorstand/Admin werden als befreit berücksichtigt (Soll/Offen/Fehlbetrag = 0).

### Hinweise
- Zeiteingaben werden beim Speichern (und in MAUI zusätzlich beim Unfocus) normalisiert; der Doppelpunkt wird dabei automatisch ergänzt.
- Wartungsvertrags-Zuordnungen (Vertrag -> Mitglied) sind als nächster Schritt vorgesehen; aktuell ist vorrangig die Pflege der Vertragsdefinitionen umgesetzt.

### Betroffene Dateien (Details)
- Core
  - `KGV.Core/Helpers/TimeText.cs`
  - `KGV.Core/Helpers/MoneyText.cs`
- Infrastructure
  - `KGV.Infrastructure/Services/SupabaseService.cs`
- WPF
  - `Views/TermineVerwaltungView.xaml`
  - `KGV.Wpf/ViewModels/TermineVerwaltungViewModel.cs`
  - `Views/BekanntmachungenVerwaltungView.xaml`
  - `Views/ImpressumView.xaml`
  - `KGV.Wpf/ViewModels/ImpressumViewModel.cs`
  - `KGV.Wpf/AppSettings.cs`
  - `KGV.Wpf/ViewModels/ArbeitsstundenViewModel.cs`
  - `KGV.Wpf/Views/ArbeitsstundenView.xaml`
  - `KGV.Wpf/ViewModels/WartungsvertraegeVerwaltungViewModel.cs`
  - `Views/WartungsvertraegeVerwaltungView.xaml`
  - `Views/WartungsvertraegeVerwaltungView.xaml.cs`
  - `KGV.Wpf/App.xaml`
  - `KGV.Wpf/ViewModels/MainWindowViewModel.cs`
  - `KGV.Wpf/Infrastructure/Services/NavigationService.cs`
- MAUI
  - `KGV.Maui/Pages/TermineAdminPage.cs`
  - `KGV.Maui/Pages/BekanntmachungenAdminPage.cs`
  - `KGV.Maui/Pages/ImpressumPage.cs`
  - `KGV.Maui/Settings/AppSettings.cs`
  - `KGV.Maui/Pages/MyArbeitsstundenPage.cs`
  - `KGV.Maui/Pages/MemberArbeitsstundenPage.cs`
  - `KGV.Maui/Pages/WartungsvertraegeAdminPage.cs`
  - `KGV.Maui/MauiProgram.cs`
  - `KGV.Maui/AdminShell.cs`


### Hinweise
- ReleaseManager greift weiterhin ausschließlich auf `Documentation/CHANGELOG.md` zu; `DEV_LOG.md` wird in diesem Workflow nicht geschrieben.

---

## 2026-03-13 – Mitglied ↔ Wartungsvertrag Zuordnung (WPF + MAUI) + Pflichtstunden-Befreiung via Vertrag

### Erledigt
- Mitgliedsbezogene Zuordnung umgesetzt (nicht zentral in der Vertragsverwaltung):
  - WPF: neuer Mitglieds-Navigationspunkt "Wartungsverträge" mit Zuweisen + Beenden (inkl. optionaler Bemerkung).
  - MAUI (AdminShell): neue Seite "Wartungsverträge" im Mitgliedskontext (analog zu Arbeitsstunden/Dokumente).
- Duplikate verhindert: derselbe Wartungsvertrag kann nicht mehrfach gleichzeitig aktiv beim selben Mitglied zugeordnet werden.
- Grundlage für Sonderfälle: Befreiung von Pflichtstunden kann über einen Wartungsvertrag mit Flag `BefreitVonPflichtstunden` abgebildet werden.
- Pflichtstunden/Fehlbetrag: Befreiung wird jetzt zusätzlich zu Role (admin/vorstand) auch über aktive Wartungsvertrags-Zuordnung geprüft.

### Hinweise
- Historisierung ist minimal über `gueltig_bis`: "Entfernen" endet die Zuordnung mit Enddatum (keine Hard-Deletes).
- Die UI bietet bewusst kein Editieren bestehender Zuordnungen (Ändern = Beenden + neu zuweisen), um Komplexität niedrig zu halten.

### Betroffene Dateien (Details)
- WPF
  - `KGV.Wpf/ViewModels/MemberWartungsvertraegeViewModel.cs` (neu)
  - `KGV.Wpf/Views/MemberWartungsvertraegeView.xaml` (neu)
  - `KGV.Wpf/Views/MemberWartungsvertraegeView.xaml.cs` (neu)
  - `KGV.Wpf/App.xaml` (DataTemplate)
  - `KGV.Wpf/ViewModels/MainWindowViewModel.cs` (Member-Navigation)
  - `KGV.Wpf/Infrastructure/Services/NavigationService.cs` (Factory)
  - `KGV.Wpf/ViewModels/ArbeitsstundenViewModel.cs` (Befreiung via Vertrag)
- MAUI
  - `KGV.Maui/Pages/MemberWartungsvertraegePage.cs` (neu)
  - `KGV.Maui/MauiProgram.cs` (DI)
  - `KGV.Maui/AdminShell.cs` (Menü)
  - `KGV.Maui/Pages/MyArbeitsstundenPage.cs` (Befreiung via Vertrag)
  - `KGV.Maui/Pages/MemberArbeitsstundenPage.cs` (Befreiung via Vertrag)

---

## 2026-03-14 – Pflichtstunden/Befreiung konsolidiert + Kapazitätsprüfung Wartungsvertrag

### Erledigt
- Zentrale fachliche Quelle eingeführt: Pflichtstunden/Befreiung/Offen/Fehlbetrag werden über `ISupabaseService.GetPflichtstundenEvaluationAsync(...)` ausgewertet (Result-Objekt mit Quelle + Grund).
- WPF + MAUI Arbeitsstunden-Ansichten nutzen diese zentrale Auswertung; keine verteilte Rollen-/Vertragslogik mehr in den Pages/ViewModels.
- Priorität fachlich explizit:
  1) aktiver Wartungsvertrag mit `BefreitVonPflichtstunden`
  2) Übergangsregel: Rolle `admin`/`vorstand`
  3) sonst Regel/Befreiungsgrund aus DB-View
- Kapazität/Regeln beim Zuweisen zentral in `SupabaseService.SaveWartungsvertragZuordnungAsync` abgesichert:
  - Duplikatschutz (gleicher Vertrag nicht mehrfach gleichzeitig aktiv pro Mitglied)
  - `MaxAktiveZuordnungen` (max. aktive Zuordnungen pro Vertrag)

### Betroffene Dateien (Details)
- Core
  - `KGV.Core/Models/PflichtstundenEvaluationResult.cs` (neu)
  - `KGV.Core/Interfaces/ISupabaseService.cs`
- Infrastructure
  - `KGV.Infrastructure/Services/SupabaseService.cs`
- WPF
  - `KGV.Wpf/ViewModels/ArbeitsstundenViewModel.cs`
- MAUI
  - `KGV.Maui/Pages/MyArbeitsstundenPage.cs`
  - `KGV.Maui/Pages/MemberArbeitsstundenPage.cs`

---

## 2026-03-13 – Restfix: Android Launcher-Icon & Updateprüfung (Footer)

### Erledigt
- Android Launcher-Icon: In der generierten Android-Manifest-Ausgabe fehlten `android:icon`/`android:roundIcon` am `<application>`-Element; Manifest-Vorlage wurde so ergänzt, dass explizit `@mipmap/appicon` / `@mipmap/appicon_round` verwendet wird.
- Updateprüfung (WPF + MAUI): Fehlerpfad liefert jetzt differenzierte, benutzerfreundliche Ursachen (z.B. Updatequelle nicht erreichbar / JSON ungültig / Konfiguration fehlt) statt nur „Updateprüfung nicht verfügbar“.

## 2026-03-13 – Restarbeiten (priorisiert): Android Update-URL, Parzellen-Dirtiness, Startseite, Saison

### Erledigt
- Android Updatepfad: `AndroidUpdateService.VersionJsonUrl` auf den ReleaseManager-Publish-Ort angepasst (`https://kgv-oberrothenbach.github.io/KGV-WPF/android/version.json`).
- WPF Mitglied-Details: Parzellen-Zuordnung/Belegungsende aktiviert jetzt `Speichern` auch ohne Stammdaten-Änderung; `Speichern` beendet Edit-Mode ohne unnötiges Mitglied-Update; `Abbrechen` fragt bei ungespeicherten Stammdaten-Änderungen nach.
- Startseite (WPF+MAUI): „Pflichtstunden“ in „Meine Arbeitsstunden“ umbenannt; Reihenfolge gemäß Vorgabe; Bekanntmachungen zeigen zunächst nur Titelliste, Inhalt erst nach Auswahl.
- Saison (WPF+MAUI): Vorauswahl bevorzugt aktuelles Jahr; `Speichern`/`Abbrechen` an Formularende verschoben.

## 2026-03-13 – Priorität 6: Impressum (Funktionsslots, Zuordnung, Editierbarkeit)

### Erledigt
- Neue Supabase-Anbindung für Impressum-Funktionsslots über Basistabelle `impressum_funktion_slot` (SlotKey + SortOrder + optional MitgliedId).
- WPF Impressum: Anzeige „Verantwortlich“, „Vorstand“ (4 Slots) und „Bauausschuss“ (3 Slots) in fester Reihenfolge; Bearbeiten-Modus mit Mitgliedsauswahl, Dirty-Tracking, Rückfrage bei Abbruch, Save/Cancel am Formularende.
- MAUI Impressum: gleiche Funktionalität inkl. Menüeintrag „Info / Impressum“, Bearbeiten-Modus mit Pickern und Save/Cancel am Formularende.
- Auswahl standardmäßig aus aktiven Mitgliedern; bereits zugeordnete inaktive Mitglieder bleiben sichtbar/auswählbar (mit Kennzeichnung).

### Hinweise
- Die Update-Diagnose-Details werden intern weiterhin protokolliert (WPF: `Debug.WriteLine`, MAUI: `ILogger` im `AndroidUpdateService`).

---

## 2026-03-13 – Restprobleme: MAUI Impressum, Bekanntmachungen-Editor, Löschen Startseite, „Meine Arbeitsstunden“

### Erledigt
- MAUI Impressum: Android-Crash „The specified child already has a parent…“ behoben (keine View/Label-Instanz mehr doppelt in zwei Layouts eingefügt).
- Bekanntmachungen (WPF + MAUI): Eingabe von rohem HTML durch einfachen Text-Editor ersetzt (Schriftgröße + Fett + Kursiv). Speicherung bleibt intern in `inhalt_html` als HTML (generiert aus Text + Formatflags).
- Startseite-Verwaltung (WPF + MAUI): Löschen für Arbeitseinsätze/Termine/Bekanntmachungen ergänzt (mit Sicherheitsabfrage + Rollencheck) + Service-Methoden zum Hard-Delete über Basistabellen.
- „Meine Arbeitsstunden“: Anzeige/Load strikt auf aktuell eingeloggten Nutzer eingeschränkt; Pflichtstunden werden über `HauptmitgliedId ?? Id` in der aktuellen Saison geladen.

### Hinweise
- Hard-Delete kann je nach DB-Regeln (RLS/FKs) fehlschlagen; UI zeigt dann „Löschen fehlgeschlagen.“ an (Deaktivieren bleibt als Alternative bestehen).
