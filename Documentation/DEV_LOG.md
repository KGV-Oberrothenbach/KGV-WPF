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

## 2026-03-14 – Bugfix-Block: Startseite-CRUD (ID=NULL), Arbeitsstunden-Auswertung (Befreiung), Uhrzeit-Autoformat, Wartungsverträge-UX

### Erledigt
- Startseite-CRUD (Termin/Bekanntmachung/Arbeitseinsatz): Insert sendet `id` nicht mehr mit (auch nicht als `null`) → DB kann ID wieder selbst generieren.
- DB-Migrationsskript ergänzt, um `id`-Default/Sequence/Identity für `arbeitseinsatz`, `termin`, `bekanntmachung` abzusichern und Sequences auf `MAX(id)` zu synchronisieren (inkl. reiner Prüfung auf `id=0`-Datensätze).
- Pflichtstunden-Auswertung: Befreiung setzt nur noch Soll/Offen auf 0, Geleistet wird weiterhin aus echten (freigegebenen) Arbeitsstunden summiert (Startseite & "Meine Arbeitsstunden" identische Logik).
- Legacy-Befreiungs-Fußtext entschärft (keine verwirrende Rollenmeldung auf der Startseite).
- WPF: Arbeitsstunden-Liste selektierbar gemacht (SelectedItem Binding explizit TwoWay / FullRow).
- Uhrzeit-Autoformat in WPF-Verwaltung wiederhergestellt (Termine/Arbeitseinsätze: Normalisierung auf HH:mm bei Fokusverlust).
- Uhrzeit-Autoformat in MAUI (Arbeitseinsätze) wiederhergestellt (Normalisierung bei Unfocused).
- Wartungsverträge: Navigation Member-Unterpunkt stabilisiert (SelectedMember wird als Parameter übergeben) + Save-Fehler werden wieder mit verständlicher Fehlermeldung bis ins UI propagiert.

### Hinweise
- DB-Skript ist defensiv/idempotent; es ändert keine vorhandenen `id=0`-Datensätze automatisch, sondern gibt nur einen NOTICE aus.

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

---

## 2026-03-16 – Admin-Flow: „Nutzer hinzufügen“ ausschließlich per OTP/E-Mail

### Erledigt
- Admin-Flow „Nutzer hinzufügen“ so umgestellt, dass ausschließlich der OTP-/E-Mail-Invite genutzt wird.
- Google/OAuth wird in diesem Flow nicht mehr geprüft/verwendet (Google-Verknüpfung bleibt Self‑Service nach erfolgreichem Login).
- Fehlermeldungen im UI fachlich verständlich gemacht und Logging für Invite-Fehler ergänzt.

---

## 2026-03-16 – Update-Pfade: Android (Play Store) vs. Windows (WPF)

### Erledigt
- Android (Play-Store-Verteilung): eigene Update-Download-/Installationsroutine deaktiviert bzw. aus der Release-App entfernt.
- Windows/WPF: bestehender Updatepfad unverändert beibehalten.
- Gemeinsame Update-Logik sauber nach Plattform getrennt, inkl. Logging welches Updateverhalten aktiv ist.

---

## 2026-03-16 – Release Manager: Settings-Dialog, breiteres Layout, Android signiertes AAB via CLI

### Erledigt
- Release Manager um lokale, benutzerspezifische Settings ergänzt (Pfade, Defaults, Android-Signing-Konfiguration; keine Secrets im Repo).
- Settings-Dialog hinzugefügt, über den alle veränderlichen Werte gepflegt und persistiert werden können.
- Android-Build im Release Manager robustiert: signiertes Release-AAB wird per `dotnet publish` erzeugt (keine VS-Automation), Signing über Settings/Passwortdateien möglich.
- MainWindow-Layout breiter/kompakter gestaltet, ohne zusätzliche Höhe; Inhalt bleibt vertikal scrollbar.

---

## 2026-03-16 – Edge Function: `kgv-invite-user` OTP-only Admin-Invite

### Erledigt
- Supabase Edge Function `kgv-invite-user` so korrigiert, dass Admin-Einladungen ausschließlich über den OTP-/E-Mail-Invite laufen.
- Provider-/Google-/OAuth-Logik aus dem Admin-Invite entfernt; `inviteMethod="otp"` wird ausgewertet.
- Logging/Fehlerantworten fachlich bereinigt (keine technischen Rohmeldungen nach außen).

### Ergänzt
- Serverseitiges Diagnoselogging für OTP-Invites ausgebaut (Request-Payload gekürzt/maskiert, gewählter Codepfad, Supabase-Error klassifiziert), ohne technische Rohmeldungen an den Client zu geben.
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

---

## 2026-03-14 – Bugfix: Startseite-CRUD (ID=0), Time-Nulls, Home "Meine Arbeitsstunden", Bekanntmachungen Auswahl-Formatierung

### Erledigt
- Startseite-Verwaltung (Arbeitseinsätze/Termine/Bekanntmachungen): Insert/Update robuster gemacht.
  - Insert sendet keine `id=0` mehr (Write-Records PK-Insert deaktiviert + nullable `Id` + Service setzt `Id=null` bei Inserts).
  - `time`-Felder werden konsequent als gültiges `HH:mm` oder `NULL` gespeichert (kein `""` mehr).
  - DB/Service-Fehlertexte werden UI-tauglich gekürzt (kein rohes JSON-Error-Payload im UI).
- Home/Startseite: Pflichtstunden/geleistete Stunden werden jetzt über denselben User→Mitglied-Auflösungsweg geladen wie "Meine Arbeitsstunden" (Fallback über `auth_user_id`) und über die zentrale Pflichtstunden-Auswertung.
- Bekanntmachungen: Formatierung (Fett/Kursiv/Schriftgröße) wird auf die markierte Textauswahl angewendet (Marker-basiert, später erweiterbar) und bestehende Teilformatierungen bleiben beim Laden/Speichern erhalten.

### Betroffene Dateien
- Core
  - `KGV.Core/Models/StartseiteArbeitseinsatzWriteRecord.cs`
  - `KGV.Core/Models/StartseiteTerminWriteRecord.cs`
  - `KGV.Core/Models/StartseiteBekanntmachungWriteRecord.cs`
  - `KGV.Core/Helpers/BekanntmachungMarkup.cs` (neu)
- Infrastructure
  - `KGV.Infrastructure/Services/SupabaseService.cs`
- WPF
  - `KGV.Wpf/ViewModels/HomeViewModel.cs`
  - `KGV.Wpf/ViewModels/TermineVerwaltungViewModel.cs`
  - `KGV.Wpf/ViewModels/ArbeitseinsaetzeVerwaltungViewModel.cs`
  - `KGV.Wpf/ViewModels/BekanntmachungenVerwaltungViewModel.cs`
  - `Views/BekanntmachungenVerwaltungView.xaml`
  - `KGV.Wpf/Views/BekanntmachungenVerwaltungView.xaml.cs`
- MAUI
  - `KGV.Maui/Pages/HomePage.xaml.cs`
  - `KGV.Maui/Pages/TermineAdminPage.cs`
  - `KGV.Maui/Pages/ArbeitseinsaetzeAdminPage.cs`
  - `KGV.Maui/Pages/BekanntmachungenAdminPage.cs`

### Hinweise
- Auswahl-Formatierung nutzt Marker im Editor-Text (`{{b}}...{{/b}}`, `{{i}}...{{/i}}`, `{{fs:N}}...{{/fs}}`), die beim Speichern in HTML-Tags übersetzt werden.
- Für eine spätere WYSIWYG-Editor-Verbesserung bleibt die Lösung bewusst minimal und ohne neue UI-Komponenten.
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
- Legacy-Rollenbefreiung ist zentral per Konfiguration vorbereitbar: `Workhours:EnableLegacyRoleBefreiung` (Default `true`).
- DB-seitige Absicherung vorbereitet (SQL-Skripte):
  - Exclusion Constraint (Überlappungen verhindern) + Trigger mit Row-Lock für atomare Kapazitätsprüfung.
  - Query zur Identifikation von Bestandsfällen (Legacy-Rolle befreit, aber kein befreiernder Vertrag aktiv).

### Betroffene Dateien (Details)
- Core
  - `KGV.Core/Models/PflichtstundenEvaluationResult.cs` (neu)
  - `KGV.Core/Interfaces/ISupabaseService.cs`
- Infrastructure
  - `KGV.Infrastructure/Services/SupabaseService.cs`
- Documentation
  - `Documentation/DB/2026-03-14_wartungsvertrag_zuordnung_db_absicherung.sql`
  - `Documentation/DB/2026-03-14_query_legacy_role_befreit_ohne_vertrag.sql`
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

---

## 2026-03-15 – Fundament: Supabase Session-Persistenz & Session-Restore (WPF + MAUI)

### Erledigt
- Zentrale Session-Verantwortung in `AuthService` ergänzt: `TryRestoreSessionAsync`, `EnsureValidSessionAsync`, `SignOutAsync`.
- Supabase-Client so konfiguriert, dass Session-Persistenz über den Supabase-.NET-Mechanismus (`SupabaseOptions.SessionHandler`) möglich ist + `AutoRefreshToken=true`.
- Lokale Session-Speicherung:
  - WPF: DPAPI-geschützt (`DpapiSupabaseSessionStore`).
  - MAUI/Android: SecureStorage (`SecureStorageSupabaseSessionStore`).
- App-Start angepasst:
  - WPF: versucht Session-Restore vor dem Login-Dialog; nur bei ungültiger Session fällt es auf Login zurück.
  - MAUI: versucht Session-Restore im Hintergrund und wechselt bei Erfolg direkt in die passende Shell; Logout löscht jetzt auch die persistierte Session.

### Hinweise
- Noch kein Umbau des OTP-Dialogs; Passwort-Login bleibt bestehen.
- Session-Restore ist defensiv: bei Fehlern wird lokal auf Login zurückgefallen.

---

## 2026-03-15 – WPF Login: OTP/Recovery nur für Passwort-Neusetzen (kein App-Einstieg)

### Erledigt
- WPF Login-Flow erweitert:
  - Regulärer Hauptweg bleibt E-Mail + Passwort.
  - Alternativer OTP-Weg: Code anfordern + Code verifizieren.
  - „Passwort vergessen“: Recovery-Code anfordern + Code verifizieren.
- Nach erfolgreicher OTP-/Recovery-Verifikation wird **sofort** ein Passwort-Neusetzen-Dialog geöffnet.
- Nach erfolgreichem Passwortsetzen wird die Session kontrolliert bereinigt und zum Login zurückgekehrt (keine automatische Weiterleitung ins `MainWindow`).

### Hinweise
- OTP-/Recovery-Verify wird bewusst ohne Persistenz ausgeführt, damit ein abgebrochener Reset-Flow nicht beim nächsten Start in eine wiederhergestellte Session „durchmarschiert“.

---

## 2026-03-15 – WPF Auth Abschluss: Inaktivität, Resume-Sessioncheck, optionaler Google-Login

### Erledigt
- Inaktivitätslogik: nach 15 Minuten ohne Benutzeraktivität wird die App geschlossen (ohne `SignOut`, Session bleibt persistiert).
- Reaktivierung/Resume: bei Fokus-Rückkehr bzw. nach Standby wird die Session zentral geprüft/ggf. refreshed; bei endgültig ungültiger Session kontrolliert zurück zum Login.
- Optionaler Direkt-Login „Mit Google anmelden“ (PKCE): Browser-Redirect auf Loopback-Callback, Exchange-Code zu Session, Rollen/UserContext wie beim Passwort-Login.

### Hinweise
- Für Google OAuth muss die Redirect-URL in Supabase/Google-Konfiguration erlaubt sein (siehe ToDos im Prompt 3/3 Ergebnis).

---

## 2026-03-15 – Stammdaten Kontakt erweitert + Nebenmitglied-Anlage repariert

### Erledigt
- Stammdaten → Kontakt: zwei neue Felder ergänzt und End-to-End durchgezogen:
  - `email_info_einwilligung` (Checkbox „E-Mail-Info“)
  - `email_rechnung_einwilligung` (Checkbox „E-Mail-Rechnung“)
  - Default jeweils `false`.
- Nebenmitglied anlegen: Root-Cause sichtbar gemacht und Flow stabilisiert.
  - Keine Übernahme der E-Mail-Adresse vom Hauptmitglied beim Anlegen (vermeidet typische Unique-Constraint Probleme).
  - Fehler werden im Service detailliert geloggt (zusätzlich lokale `error.log`) und in der UI als konkrete Ursache angezeigt.

### Hinweise
- Supabase Migration als Datei ergänzt (`supabase/migrations/...sql`) – ausführen nur falls die Spalten in der DB noch fehlen.

---

## 2026-03-15 – WPF: E-Mail-Änderung nur über separaten OTP-Flow

### Erledigt
- Stammdaten: E-Mail-Feld bleibt auch im Bearbeiten-Modus read-only; normales Speichern ändert die E-Mail nicht.
- Neuer separater Einstieg „Mailadresse ändern“ (Dialog):
  - neue E-Mail eingeben
  - OTP-Code anfordern (Supabase Auth `Update(email)`)
  - OTP in der App eingeben und codebasiert verifizieren (`VerifyOTP` mit `EmailChange`)
- Nach erfolgreicher Verifikation wird die neue E-Mail zusätzlich im `mitglied.email` gespeichert (dedizierte Service-Methode, nicht über Standard-Save) und die Stammdaten werden neu geladen.

### Absicherung / Korrektheit
- Fachliche Differenzierung Kontakt-Mail vs. Login-Mail:
  - `auth_user_id == null`: E-Mail ist wieder normal in den Stammdaten editierbar und wird über `UpdateMitgliedAsync` gespeichert.
  - `auth_user_id != null`: E-Mail bleibt im normalen Bearbeiten gesperrt (keine Stammdaten-Änderung).
- Button/Flow ist nur sichtbar/aktiv, wenn das angezeigte Mitglied dem aktuell eingeloggten Auth-User entspricht (`mitglied.auth_user_id == current user id`).
  - verhindert den fachlich falschen Eindruck, man könne die Auth-Mail eines fremden Mitglieds ändern.
- Verifikation gilt nur dann als erfolgreich, wenn Supabase nach `VerifyOTP(EmailChange)` die neue E-Mail im Session-User auch tatsächlich zurückliefert.
  - andernfalls wird ein klarer Hinweis angezeigt (typisch: `secure_email_change` / zusätzliche Bestätigung erforderlich).

- Google/OAuth:
  - Falls das eigene Konto via Google/OAuth erkannt wird, wird der OTP-Mailänderungs-Flow nicht angeboten (Hinweis in der UI), um keinen falschen Eindruck zu erzeugen.

### Hinweise
- Wenn in Supabase `secure_email_change` aktiv ist, kann zusätzlich eine Bestätigung der alten Adresse nötig sein; der App-Flow ist codebasiert vorbereitet, aber projektseitige Einstellungen müssen geprüft werden.

---

## 2026-03-15 – Release Manager: gemeinsamer Windows/Android Master-Release (Umbau)

### Erledigt
- `Documentation/releases.json` wird durch den ReleaseManager nun als **Master-Release-Liste** gelesen/geschrieben (1 Release-Version, mehrere Plattformen).
  - Abwärtskompatibel: altes JSON-Format (nur Release Notes) wird beim Lesen weiterhin akzeptiert und mit Default-Plattformen gemappt.
- ReleaseManager speichert beim „Release-Text speichern“ zusätzlich Plattform-Metadaten:
  - Windows: DirectDownload (wie bisher)
  - Android: PlayStore (PackageName, Track, PublishingStatus, StoreUrl, ReleaseName, optional VersionCode)
- Gemeinsame Versionsführung im Workflow:
  - Bei „Beide“ gilt die Windows-Version als Master-Version und wird auch für Android verwendet.
- Zusätzlich: Warnung im ReleaseManager, wenn die aktuell eingelesenen Windows-/Android-Versionen auseinanderlaufen.
- UI: Fenster breiter/kompakter + vertikales Scrollen, damit alle Bereiche auch bei kleiner Fensterhöhe erreichbar bleiben.
- Save-Validierung getrennt: „Release-Text speichern“ erzwingt keine Android-Build/VersionCode-Validierung mehr (Android kann als Entwurf ohne AAB gespeichert werden).
- Start-Validierung getrennt: „Release starten“ leitet fehlenden Android-VersionCode (Build) bei Bedarf automatisch aus der aktuellen csproj ab (Current+1) und startet dann den AAB-Build; keine Prüfung auf bereits vorhandene Artefakte.
- Komfort/Qualität: Android `PackageName` (ApplicationId) wird beim Laden automatisch aus dem Projekt vorbelegt (Fallback: zuletzt gespeicherter Wert) und `ReleaseName` wird aus Version + PlayTrack automatisch generiert, solange nicht manuell überschrieben.
- Android-Build im ReleaseManager: Umstellung auf **AAB** und Play-Store-orientiertes `version.json` (kein Endnutzerdownload-Link für Android).
- Plattform-Status kann nach erfolgreichem Build im Release-Katalog aktualisiert werden (z.B. „gebaut“, „AAB erstellt“).
