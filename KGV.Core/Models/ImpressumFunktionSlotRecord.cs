using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models;

// Basistabelle für feste Funktions-Slots im Impressum.
// Schreiben erfolgt auf diese Tabelle; Anzeige/Join wird im Client über Mitglied-Daten ergänzt.
[Table("impressum_funktion_slot")]
public sealed class ImpressumFunktionSlotRecord : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public long Id { get; set; }

    // Stabiler, eindeutiger Schlüssel (notwendig u.a. wegen doppelter Labels wie "Bauausschuß").
    [Column("slot_key")]
    public string? SlotKey { get; set; }

    // Anzeigetext der Funktion (z.B. "Vorstandsvorsitzender").
    [Column("funktion")]
    public string? Funktion { get; set; }

    // Stabile Sortierung in der DB (zusätzlich zur festen Reihenfolge im Client).
    [Column("sort_order")]
    public int SortOrder { get; set; }

    // Optional zugeordnetes Mitglied.
    [Column("mitglied_id")]
    public int? MitgliedId { get; set; }
}
