# KGV Oberrothenbach – Architektur

## Projektziel

Desktop-Anwendung zur Verwaltung eines Kleingartenvereins mit:

- Login (Supabase Auth)
- Mitgliederverwaltung (Stammdaten, Arbeitsstunden, Parzellen, Dokumente)
- Exportfunktionen
- Rollenbasiertem Zugriff (Admin)

---

## Technologiestack

- WPF (.NET 8 LTS)
- CommunityToolkit.Mvvm
- Supabase .NET SDK
- Dependency Injection (Microsoft.Extensions.DependencyInjection)

---

## Architekturprinzipien

- MVVM Pattern
- Keine SQL-Logik im UI
- Services kapseln Datenzugriff
- DTOs für Supabase
- UI-Modelle getrennt von DB-Modellen
- Navigation über ContentControl
- Keine Reflection
- Keine RPC-Experimente
- Saubere Trennung von Verantwortlichkeiten

---

## Projektstruktur

KGV  
├── Views  
├── ViewModels  
├── Services  
├── Models  
├── Infrastructure  
├── Documentation  

---

## Navigation

MainWindow enthält:

ContentControl → bindet an CurrentViewModel

Flow:

App  
 └── MainWindow  
       └── MainViewModel  
             └── CurrentViewModel (Login oder Dashboard)

---

## Login-Verhalten

- E-Mail wird lokal gespeichert
- Wenn E-Mail leer → Fokus in E-Mail-Feld
- Wenn E-Mail vorhanden → Fokus in Passwort
- ENTER in jedem Feld löst Login aus
- Nach Login → Wechsel zum Hauptbereich

---

## Dashboard-Layout

Zweiteilig:

Links:
- Saison Dropdown (Standard: aktuelles Jahr)
- Mitgliedersuche (Name/Vorname)
- Dynamische Navigation:
  - Stammdaten
  - Arbeitsstunden
  - Parzellen → Strom, Wasser, Dokumente
  - Dokumente
  - Mitglied Neu
  - Admin-Menü (sichtbar nur für Admin)
  - Export (immer sichtbar)

Rechts:
- Arbeits-/Anzeige-Bereich

---

## Stammdaten – Felder

### Sichtbare Felder für UI

- Vorname  
- Nachname  
- Straße  
- PLZ  
- Ort  
- Telefon  
- E-Mail  
- Bemerkungen (optional, mehrzeilig)  
- Whatsapp_Einwilligung (Checkbox)  

### Interne / Admin-Felder

- auth_user_id (UUID/Text, nicht editierbar)  
- ist_kgv (Boolean, intern für Umlagen, nicht editierbar)  
- aktiv (Boolean, Mitglied aktiv/inaktiv)  
- role (Text/Enum, nur Admin sichtbar, änderbar über Dropdown)  

---

## Rollenmodell

- Admin: Vollzugriff auf alle Funktionen, kann Rollen ändern  
- Vorstand: Zugriff auf relevante Daten (Arbeitsstunden, Parzellen)  
- Mitglied: Zugriff nur auf eigene Daten  
- Berechtigungen über Supabase Row-Level Policies umgesetzt  

---

## Datenfluss

UI → ViewModel → Service → Supabase

---

## Nächste Schritte

1. MemberDTO / ViewModel für Stammdaten erstellen  
2. Login und Password-Visible-Funktionalität stabilisieren  
3. Dashboard-Navigation dynamisch anpassen  
4. Arbeitsstunden- und Parzellenservices vorbereiten  
5. Rollen- und Policy-Handling in Services integrieren
