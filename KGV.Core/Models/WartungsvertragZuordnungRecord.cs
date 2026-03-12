using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models;

[Table("wartungsvertrag_zuordnungen")]
public sealed class WartungsvertragZuordnungRecord : BaseModel
{
    [PrimaryKey("id")]
    [Column("id")]
    public long Id { get; set; }

    [Column("wartungsvertrag_id")]
    public long WartungsvertragId { get; set; }

    [Column("hauptmitglied_id")]
    public int HauptmitgliedId { get; set; }

    [Column("gueltig_ab")]
    public DateTime GueltigAb { get; set; }

    [Column("gueltig_bis")]
    public DateTime? GueltigBis { get; set; }

    [Column("bemerkung")]
    public string? Bemerkung { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
