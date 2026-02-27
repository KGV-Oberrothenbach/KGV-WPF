using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace KGV.Core.Models
{
    [Table("arbeitsstunde")]
    public class ArbeitsstundeRecord : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public int Id { get; set; }

        [Column("locked_by_user_id")]
        public string LockedByUserId { get; set; } = string.Empty;

        [Column("locked_at")]
        public DateTime? LockedAt { get; set; }
    }
}
