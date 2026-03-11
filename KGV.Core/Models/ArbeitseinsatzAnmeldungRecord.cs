using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models;

[Table("arbeitseinsatz_anmeldung")]
public sealed class ArbeitseinsatzAnmeldungRecord : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public long Id { get; set; }

    [Column("arbeitseinsatz_id")]
    public long ArbeitseinsatzId { get; set; }

    [Column("mitglied_id")]
    public int MitgliedId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
