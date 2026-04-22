using System.ComponentModel.DataAnnotations;

namespace DocumEntum.Data
{
    public class WorkflowState
    {
        [Key]
        public int Id { get; set; }

        public int WorkflowId { get; set; }
        public virtual Workflow Workflow { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsInitial { get; set; }
        public bool IsFinal { get; set; }

        public int Order { get; set; }
    }
}
