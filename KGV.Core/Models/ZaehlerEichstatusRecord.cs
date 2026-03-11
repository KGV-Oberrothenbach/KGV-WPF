using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models;

[Table("v_zaehler_eichstatus")]
public sealed class ZaehlerEichstatusRecord : BaseModel
{
    [PrimaryKey("zaehler_id", false)]
    [Column("zaehler_id")]
    public long ZaehlerId { get; set; }

    [Column("parzelle_id")]
    public long? ParzelleId { get; set; }

    [Column("zaehler_typ")]
    public short? ZaehlerTyp { get; set; }

    [Column("anlage")]
    public string? Anlage { get; set; }

    [Column("garten_nr")]
    public int? GartenNr { get; set; }

    [Column("medium")]
    public string? Medium { get; set; }

    [Column("zaehlernummer")]
    public string? Zaehlernummer { get; set; }

    [Column("eichdatum")]
    public DateTime? Eichdatum { get; set; }

    [Column("eichfaellig_am")]
    public DateTime? EichfaelligAm { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("tage_bis_faellig")]
    public int? TageBisFaellig { get; set; }
}
