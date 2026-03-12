using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models
{
    [Table("saison")]
    public class SaisonRecord : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public int Id { get; set; }

        [Column("jahr")]
        public int Jahr { get; set; }

        [Column("pflichtstunden_soll")]
        public decimal PflichtstundenSoll { get; set; }

        [Column("euro_pro_fehlstunde")]
        public decimal EuroProFehlstunde { get; set; }

        [Column("bemerkung")]
        public string? Bemerkung { get; set; }
    }
}
