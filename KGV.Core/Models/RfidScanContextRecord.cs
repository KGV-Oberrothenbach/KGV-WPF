using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models;

[Table("v_rfid_scan_context")]
public sealed class RfidScanContextRecord : BaseModel
{
    [PrimaryKey("rfid_tag_uid", false)]
    [Column("rfid_tag_uid")]
    public string RfidTagUid { get; set; } = string.Empty;

    [Column("parzelle_id")]
    public long? ParzelleId { get; set; }

    [Column("anlage")]
    public string? Anlage { get; set; }

    [Column("garten_nr")]
    public int? GartenNr { get; set; }

    [Column("medium")]
    public string? Medium { get; set; }

    [Column("aktiver_zaehler_id")]
    public long? AktiverZaehlerId { get; set; }

    [Column("zaehlernummer")]
    public string? Zaehlernummer { get; set; }

    [Column("eichdatum")]
    public DateTime? Eichdatum { get; set; }

    [Column("eichfaellig_am")]
    public DateTime? EichfaelligAm { get; set; }

    [Column("eingebaut_am")]
    public DateTime? EingebautAm { get; set; }

    [Column("ausgebaut_am")]
    public DateTime? AusgebautAm { get; set; }

    [Column("status")]
    public string? Status { get; set; }
}
