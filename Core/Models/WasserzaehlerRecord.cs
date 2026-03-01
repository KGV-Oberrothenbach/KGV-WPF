// File: Core/Models/WasserzaehlerRecord.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace KGV.Core.Models
{
    [Table("wasserzaehler")]
    public class WasserzaehlerRecord : BaseModel
    {
        [PrimaryKey("id", false)]
        [Column("id")]
        public long Id { get; set; }

        [Column("parzelle_id")]
        public long ParzelleId { get; set; }

        [Column("zaehlernummer")]
        public string Zaehlernummer { get; set; } = string.Empty;

        [Column("eichdatum")]
        public DateTime Eichdatum { get; set; }

        [Column("eingebaut_am")]
        public DateTime EingebautAm { get; set; }

        [Column("ausgebaut_am")]
        public DateTime? AusgebautAm { get; set; }
    }
}
