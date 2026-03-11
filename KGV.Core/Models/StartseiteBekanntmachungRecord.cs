using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models;

[Table("v_startseite_bekanntmachungen")]
public sealed class StartseiteBekanntmachungRecord : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public long Id { get; set; }

    [Column("titel")]
    public string? Titel { get; set; }

    [Column("inhalt_html")]
    public string? InhaltHtml { get; set; }

    [Column("sichtbar_ab")]
    public DateTime? SichtbarAb { get; set; }

    [Column("sichtbar_bis")]
    public DateTime? SichtbarBis { get; set; }

    [Column("sort_order")]
    public int? SortOrder { get; set; }
}
