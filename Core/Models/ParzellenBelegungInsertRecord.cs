// File: Core/Models/ParzellenBelegungInsertRecord.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace KGV.Core.Models
{
    /// <summary>
    /// Insert-Model ohne PrimaryKey, damit PostgREST keine explizite id (z.B. 0) mitsendet.
    /// </summary>
    [Table("parzellen_belegung")]
    public class ParzellenBelegungInsertRecord : BaseModel
    {
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
