using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace KGV.Infrastructure.Models
{
    [Table("app_user")]
    public class AppUserRecord : BaseModel
    {
        [PrimaryKey("user_id", false)]
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("mitglied_id")]
        public long? MitgliedId { get; set; }

        [Column("role")]
        public string? Role { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
