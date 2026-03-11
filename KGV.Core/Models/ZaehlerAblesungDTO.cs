// File: Core/Models/ZaehlerAblesungDTO.cs
using System;

namespace KGV.Core.Models
{
    public sealed class ZaehlerAblesungDTO
    {
        public long AblesungId { get; set; }
        public long ZaehlerId { get; set; }
        public DateTime Ablesedatum { get; set; }
        public decimal Stand { get; set; }
        public string Zaehlernummer { get; set; } = string.Empty;
        public DateTime Eichdatum { get; set; }

        public string? FotoPfad { get; set; }
    }
}
