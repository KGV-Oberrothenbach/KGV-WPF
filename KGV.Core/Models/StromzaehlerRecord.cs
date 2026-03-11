// File: Core/Models/StromzaehlerRecord.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace KGV.Core.Models
{
    [Table("stromzaehler")]
    public class StromzaehlerRecord : BaseModel
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
