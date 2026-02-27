using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace KGV.Core.Models
{
    [Table("parzellen_belegung")]
    public class ParzellenBelegungRecord : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public int Id { get; set; }

        [Column("parzelle_id")]
        public int ParzelleId { get; set; }

        [Column("mitglied_id")]
        public int MitgliedId { get; set; }

        [Column("von_datum")]
        public DateTime? VonDatum { get; set; }

        [Column("bis_datum")]
        public DateTime? BisDatum { get; set; }
    }
}
