using System.ComponentModel.DataAnnotations;

namespace DocumEntum.Data
{
    /// <summary>
    /// Бизнес-процесс (схема согласования, маршрут документа).
    /// Содержит набор состояний и переходов между ними.
    /// </summary>
    public class Workflow
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        /// <summary>Активен ли процесс (можно отключать устаревшие маршруты).</summary>
        public bool IsActive { get; set; } = true;
        // <summary>Коллекция состояний, принадлежащих процессу.</summary>
        public virtual ICollection<WorkflowState> States { get; set; } = new List<WorkflowState>();
        /// <summary>
        /// тип документа
        /// </summary>
        public int DocumentTypeId { get; set; }
        public virtual DocumentType DocumentType { get; set; } = null!;
    }
}
