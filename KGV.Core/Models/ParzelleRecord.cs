// File: Core/Models/ParzelleRecord.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models
{
    [Table("parzelle")]
    public class ParzelleRecord : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public int Id { get; set; }

        [Column("garten_nr")]
        public string GartenNr { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("rfid_strom")]
        public string? RfidStrom { get; set; }

        [Column("rfid_wasser")]
        public string? RfidWasser { get; set; }

        // Alias (du nutzt im VM/DTO "Anlage")
        public string Anlage
        {
            get => Name;
            set => Name = value;
        }
    }
}
