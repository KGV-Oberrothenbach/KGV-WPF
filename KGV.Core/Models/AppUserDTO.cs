using System;

namespace KGV.Core.Models
{
    public sealed class AppUserDTO
    {
        public Guid UserId { get; set; }
        public long? MitgliedId { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
