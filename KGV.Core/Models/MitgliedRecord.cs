using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace KGV.Core.Models
{
    [Table("mitglied")]
    public class MitgliedRecord : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public int Id { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("vorname")]
        public string? Vorname { get; set; }

        // DB-Spalte heißt "name" (Nachname)
        [Column("name")]
        public string? Name { get; set; }

        [Column("role")]
        public string? Role { get; set; }

        [Column("auth_user_id")]
        public Guid? AuthUserId { get; set; }

        // ===== Stammdaten =====
        [Column("geburtsdatum")]
        public DateTime? Geburtsdatum { get; set; }

        // DB-Spalte heißt "adresse" (DTO heißt Strasse)
        [Column("adresse")]
        public string? Adresse { get; set; }

        [Column("plz")]
        public string? Plz { get; set; }

        [Column("ort")]
        public string? Ort { get; set; }

        [Column("telefon")]
        public string? Telefon { get; set; }

        [Column("handy")]
        public string? Handy { get; set; }

        // DB-Spalte heißt "bemerkung" (DTO heißt Bemerkungen)
        [Column("bemerkung")]
        public string? Bemerkung { get; set; }

        [Column("whatsapp_einwilligung")]
        public bool WhatsappEinwilligung { get; set; }

        [Column("mitglied_seit")]
        public DateTime? MitgliedSeit { get; set; }

        [Column("mitglied_ende")]
        public DateTime? MitgliedEnde { get; set; }

        [Column("aktiv")]
        public bool Aktiv { get; set; }

        [Column("hauptmitglied_id")]
        public int? HauptmitgliedId { get; set; }

        // ===== Locking (WICHTIG: Spaltennamen aus DB) =====
        // DB: lockedbyuserid / lockat
        [Column("lockedbyuserid")]
        public Guid? LockedByUserId { get; set; }

        [Column("lockat")]
        public DateTime? LockedAt { get; set; }
    }
}
