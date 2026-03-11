// File: Core/Models/NebenmitgliedCreateDTO.cs
namespace KGV.Core.Models
{
    public sealed class NebenmitgliedCreateDTO
    {
        public int HauptmitgliedId { get; set; }
        public string Vorname { get; set; } = string.Empty;
        public string Nachname { get; set; } = string.Empty;
        public bool AdresseUebernehmen { get; set; }
    }
}
