using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models;

[Table("v_startseite_arbeitseinsatz")]
public sealed class StartseiteArbeitseinsatzRecord : BaseModel
{
    [PrimaryKey("id", false)]
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

    [Column("treffpunkt")]
    public string? Treffpunkt { get; set; }

    [Column("max_teilnehmer")]
    public int? MaxTeilnehmer { get; set; }

    [Column("stunden_wert")]
    public decimal? StundenWert { get; set; }

    [Column("sichtbar_ab")]
    public DateTime? SichtbarAb { get; set; }

    [Column("sichtbar_bis")]
    public DateTime? SichtbarBis { get; set; }

    [Column("anmeldung_bis")]
    public DateTime? AnmeldungBis { get; set; }

    [Column("angemeldet_count")]
    public int? AngemeldetCount { get; set; }

    [Column("freie_plaetze")]
    public int? FreiePlaetze { get; set; }
}
