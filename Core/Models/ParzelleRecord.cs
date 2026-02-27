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
    }
}
