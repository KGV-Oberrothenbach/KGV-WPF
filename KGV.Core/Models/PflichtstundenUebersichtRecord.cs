using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models;

[Table("v_pflichtstunden_uebersicht")]
public sealed class PflichtstundenUebersichtRecord : BaseModel
{
    [Column("hauptmitglied_id")]
    public int HauptmitgliedId { get; set; }

    [Column("saison_id")]
    public int SaisonId { get; set; }

    [Column("jahr")]
    public int Jahr { get; set; }

    [Column("sollstunden")]
    public decimal Sollstunden { get; set; }

    [Column("geleistet")]
    public decimal Geleistet { get; set; }

    [Column("offen")]
    public decimal Offen { get; set; }

    [Column("fehlbetrag")]
    public decimal Fehlbetrag { get; set; }

    [Column("regelgrund")]
    public string? Regelgrund { get; set; }

    [Column("befreiungsgrund")]
    public string? Befreiungsgrund { get; set; }
}
