

# Architektur-Entscheidungen

## 2026-02-18 – Neustart

Projekt neu gestartet, um Altlasten und inkonsistente Architektur zu vermeiden.

---

## MVVM Framework

CommunityToolkit.Mvvm gewählt.

Grund:
- Weniger Boilerplate
- Klare Struktur
- Professionelle Basis

---

## Navigation

ContentControl + ViewModel Switching.

Kein Frame.
Grund:
- Flexibler
- Besser MVVM-konform

---

## Supabase

Verwendung des offiziellen .NET SDK.
Kein direktes SQL im UI.
Keine Reflection-Fallbacks.

---

## .NET Version

.NET 8.0 LTS gewählt.
Grund:
- Stabil
- Langzeitunterstützung
- Gute SDK-Kompatibilität