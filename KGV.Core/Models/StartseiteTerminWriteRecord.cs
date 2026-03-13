using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models;

[Table("termin")]
public sealed class StartseiteTerminWriteRecord : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public long Id { get; set; }

    [Column("titel")]
    public string? Titel { get; set; }

    [Column("beschreibung")]
    public string? Beschreibung { get; set; }

    [Column("datum")]
    public DateTime? Datum { get; set; }

    [Column("start_uhrzeit")]
    public string? StartUhrzeit { get; set; }

    [Column("end_uhrzeit")]
    public string? EndUhrzeit { get; set; }

    [Column("sichtbar_ab")]
    public DateTime? SichtbarAb { get; set; }

    [Column("sichtbar_bis")]
    public DateTime? SichtbarBis { get; set; }
}
