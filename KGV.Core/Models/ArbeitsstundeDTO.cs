// File: Core/Models/ArbeitsstundeDTO.cs
using System;

namespace KGV.Core.Models
{
    public sealed class ArbeitsstundeDTO
    {
        public int Id { get; set; }

        public int MitgliedId { get; set; }
        public string Vorname { get; set; } = string.Empty;
        public string Nachname { get; set; } = string.Empty;

        public DateTime Datum { get; set; }

        public int SaisonId { get; set; }
        public int SaisonJahr { get; set; }

        public decimal Stunden { get; set; }
        public string Beschreibung { get; set; } = string.Empty;

        public string? Status { get; set; }

        public bool Freigegeben { get; set; }
        public DateTime? FreigegebenAm { get; set; }
        public int? FreigegebenVonId { get; set; }
        public string? FreigegebenVonName { get; set; }
    }
}
