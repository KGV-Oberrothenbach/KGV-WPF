using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models
{
    [Table("dokument")]
    public class DokumentRecord : BaseModel
    {
        [PrimaryKey("id", false)]
        [Column("id")]
        public long Id { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("mitglied_id")]
        public int? MitgliedId { get; set; }

        [Column("parzelle_id")]
        public int? ParzelleId { get; set; }

        [Column("bucket")]
        public string? Bucket { get; set; }

        [Column("storage_path")]
        public string? StoragePath { get; set; }

        [Column("titel")]
        public string? Titel { get; set; }

        [Column("dateiname")]
        public string? Dateiname { get; set; }

        [Column("mime_type")]
        public string? MimeType { get; set; }

        [Column("size_bytes")]
        public long? SizeBytes { get; set; }
    }
}
