// File: Core/Models/ParzelleSelectionDTO.cs
using System;

namespace KGV.Core.Models
{
    /// <summary>
    /// UI-DTO für die Parzellen-Auswahl (ComboBox) inkl. Status/Anzeige.
    /// </summary>
    public class ParzelleSelectionDTO
    {
        public int ParzelleId { get; set; }
        public string GartenNr { get; set; } = "";
        public string Anlage { get; set; } = "";

        /// <summary>
        /// "Frei", "Frei (bis ...)", "Belegt"
        /// </summary>
        public string StatusText { get; set; } = "";

        public string DisplayText
        {
            get
            {
                var basis = string.IsNullOrWhiteSpace(Anlage) ? GartenNr : $"{GartenNr} - {Anlage}";
                return string.IsNullOrWhiteSpace(StatusText) ? basis : $"{basis} ({StatusText})";
            }
        }
    }
}
