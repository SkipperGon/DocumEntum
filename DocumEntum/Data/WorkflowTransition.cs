using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumEntum.Data
{
    public class WorkflowTransition
    {
        [Key]
        public int Id { get; set; }

        public int WorkflowId { get; set; }
        public virtual Workflow Workflow { get; set; } = null!;

        public int FromStateId { get; set; }
        public virtual WorkflowState FromState { get; set; } = null!;

        public int ToStateId { get; set; }
        public virtual WorkflowState ToState { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string ActionName { get; set; } = string.Empty; // "Submit", "Approve", "Reject"

        // Какие роли могут выполнить переход (храним имена ролей через запятую)
        public string? AllowedRoles { get; set; }

        // Или конкретные позиции (ID должностей)
        public string? AllowedPositionIds { get; set; }
    }
}
