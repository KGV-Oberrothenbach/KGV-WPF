using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace KGV.Core.Models
{
    [Table("arbeitsstunde")]
    public class ArbeitsstundeRecord : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public int Id { get; set; }

        [Column("mitglied_id")]
        public int MitgliedId { get; set; }

        [Column("saison_id")]
        public int SaisonId { get; set; }

        [Column("datum")]
        public DateTime Datum { get; set; }

        [Column("stunden")]
        public decimal Stunden { get; set; }

        [Column("art_der_arbeit")]
        public string ArtDerArbeit { get; set; } = string.Empty;

        [Column("status")]
        public string? Status { get; set; }

        [Column("freigegeben")]
        public bool Freigegeben { get; set; }

        [Column("genehmigt_am")]
        public DateTime? GenehmigtAm { get; set; }

        [Column("genehmigt_von")]
        public int? GenehmigtVon { get; set; }

        // ===== Locking (Spaltennamen aus DB) =====
        [Column("lockedbyuserid")]
        public string? LockedByUserId { get; set; }

        [Column("lockat")]
        public DateTime? LockedAt { get; set; }
    }
}
