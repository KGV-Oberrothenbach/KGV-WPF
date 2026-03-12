using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models;

[Table("wartungsvertraege")]
public sealed class WartungsvertragRecord : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public long Id { get; set; }

    [Column("titel")]
    public string? Titel { get; set; }

    [Column("beschreibung")]
    public string? Beschreibung { get; set; }

    [Column("bereich")]
    public string? Bereich { get; set; }

    [Column("max_aktive_zuordnungen")]
    public int MaxAktiveZuordnungen { get; set; }

    [Column("befreit_von_pflichtstunden")]
    public bool BefreitVonPflichtstunden { get; set; }

    [Column("aktiv")]
    public bool Aktiv { get; set; }

    [Column("bemerkung")]
    public string? Bemerkung { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
