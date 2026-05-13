using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumEntum.Data
{
    /// <summary>
    /// Архивная версия утверждённого документа (снимок метаданных и пути к файлу до очередного утверждения).
    /// </summary>
    public class DocumentVersion
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Утверждённый документ (актуальная запись в каталоге).</summary>
        public int DocumentId { get; set; }
        public virtual Document Document { get; set; } = null!;

        /// <summary>Номер версии на момент архивации (до замены новым утверждением).</summary>
        public int VersionNumber { get; set; }

        [Required]
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(260)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string FileExtension { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [Required]
        [MaxLength(200)]
        public string ContentType { get; set; } = string.Empty;

        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> ExtraAttributes { get; set; } = new();

        public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    }
}
