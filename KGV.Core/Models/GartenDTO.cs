namespace KGV.Core.Models
{
    public class GartenDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        // Parzellen-/Garten-Nummer für Anzeige
        public string Nummer { get; set; } = "";
    }
}
