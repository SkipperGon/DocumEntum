using System.ComponentModel.DataAnnotations;

namespace DocumEntum.Data
{
    /// <summary>
    /// Состояние (статус) документа в рамках workflow.
    /// Например: «Черновик», «На подписи», «Утверждён».
    /// </summary>
    public class WorkflowState
    {
        [Key]
        public int Id { get; set; }
        /// <summary>Идентификатор процесса, к которому принадлежит состояние.</summary>
        public int WorkflowId { get; set; }
        public virtual Workflow Workflow { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        /// <summary>Является ли это состояние начальным (черновик).</summary>
        public bool IsInitial { get; set; }
        /// <summary>Является ли состояние конечным (процесс завершён).</summary>
        public bool IsFinal { get; set; }
        /// <summary>Порядковый номер для визуализации (опционально).</summary>
        public int Order { get; set; }
    }
}
