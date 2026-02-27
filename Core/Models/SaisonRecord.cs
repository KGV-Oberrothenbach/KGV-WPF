using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace KGV.Core.Models
{
    [Table("saison")]
    public class SaisonRecord : BaseModel
    {
        [Column("jahr")]
        public int Jahr { get; set; }
    }
}
