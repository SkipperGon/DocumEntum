using System.ComponentModel.DataAnnotations;

namespace DocumEntum.Data
{
    public class DocumentHistory
    {
        [Key]
        public int Id { get; set; }

        public int DocumentId { get; set; }
        public virtual Document Document { get; set; } = null!;

        public int? FromStateId { get; set; }
        public int ToStateId { get; set; }

        public int ActionById { get; set; }
        public virtual Employee ActionBy { get; set; } = null!;

        public string ActionName { get; set; } = string.Empty;
        public string? Comment { get; set; }

        public DateTime ActionAt { get; set; } = DateTime.UtcNow;
    }
}
