using System.ComponentModel.DataAnnotations;

namespace DocumEntum.Data
{
    public class Workflow
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<WorkflowState> States { get; set; } = new List<WorkflowState>();
    }
}
