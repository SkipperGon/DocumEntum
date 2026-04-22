using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumEntum.Data
{
    public class Document
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        public string? Content { get; set; }

        public int AuthorId { get; set; }
        public virtual Employee Author { get; set; } = null!;

        public int CurrentStateId { get; set; }
        public virtual WorkflowState CurrentState { get; set; } = null!;

        public int WorkflowId { get; set; }
        public virtual Workflow Workflow { get; set; } = null!;

        public int? DepartmentId { get; set; }
        public virtual Department? Department { get; set; }

        // Динамические атрибуты в JSONB
        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> ExtraAttributes { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
