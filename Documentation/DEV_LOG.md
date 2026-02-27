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
