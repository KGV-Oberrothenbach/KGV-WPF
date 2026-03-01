using System;

namespace KGV.Core.Models
{
    public sealed class DocumentInfo
    {
        public string Name { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public long? Size { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
