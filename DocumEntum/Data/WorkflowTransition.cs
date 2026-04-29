using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumEntum.Data
{
    /// <summary>
    /// Переход между состояниями workflow.
    /// Определяет, из какого состояния в какое можно перейти, какое действие при этом выполняется,
    /// а также кто (роли или должности) имеет право выполнить переход.
    /// </summary>
    public class WorkflowTransition
    {
        [Key]
        public int Id { get; set; }
        /// <summary>Идентификатор процесса.</summary>
        public int WorkflowId { get; set; }
        public virtual Workflow Workflow { get; set; } = null!;
        /// <summary>Исходное состояние (откуда).</summary>
        public int FromStateId { get; set; }
        public virtual WorkflowState FromState { get; set; } = null!;
        /// <summary>Целевое состояние (куда).</summary>
        public int ToStateId { get; set; }
        public virtual WorkflowState ToState { get; set; } = null!;
        /// <summary>
        /// Действие, вызывающее переход (например, "Submit", "Approve", "Reject").
        /// Используется в коде для идентификации нажатой кнопки.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string ActionName { get; set; } = string.Empty; // "Submit", "Approve", "Reject"
        /// <summary>
        /// Список ролей (через запятую), которые могут выполнить переход.
        /// Пример: "Admin,Manager"
        /// </summary>
        public string? AllowedRoles { get; set; }
        /// <summary>
        /// Список идентификаторов должностей (через запятую), которые могут выполнить переход.
        /// Пример: "1,5,7"
        /// </summary>
        public string? AllowedPositionIds { get; set; }
    }
}
