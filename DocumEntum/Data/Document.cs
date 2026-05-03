using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumEntum.Data
{
    /// <summary>
    /// Документ в системе.
    /// Поддерживает динамические атрибуты через JSONB поле ExtraAttributes.
    /// Привязан к workflow, текущему состоянию, автору и отделу.
    /// </summary>
    public class Document
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;
        // Оригинальное имя с расширением
        public string FileName { get; set; } = string.Empty;
        // GUID без расширение (например, "123e4567-e89b-12d3-a456-426614174000")
        public string StoredFileName { get; set; } = string.Empty;
        // Расширение без точки (pdf, docx)
        public string FileExtension { get; set; } = string.Empty;
        // Размер в байтах
        public long FileSize { get; set; }
        // MIME-тип
        public string ContentType { get; set; } = string.Empty;     

        /// <summary>Идентификатор автора (сотрудника).</summary>
        public int AuthorId { get; set; }
        public virtual Employee Author { get; set; } = null!;
        /// <summary>Идентификатор текущего состояния workflow.</summary>
        public int CurrentStateId { get; set; }
        public virtual WorkflowState CurrentState { get; set; } = null!;
        /// <summary>Идентификатор используемого процесса workflow.</summary>
        public int WorkflowId { get; set; }
        public virtual Workflow Workflow { get; set; } = null!;
        /// <summary>Идентификатор отдела, к которому привязан документ (может быть null).</summary>
        public int? DepartmentId { get; set; }
        public virtual Department? Department { get; set; }

        // <summary>
        /// Динамические атрибуты документа в формате JSONB.
        /// Позволяет хранить произвольную структуру полей (например, «Сумма», «СНИЛС», «Номер договора»).
        /// </summary>
        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> ExtraAttributes { get; set; } = new();
        /// <summary>Дата создания документа.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>Дата последнего обновления.</summary>
        public DateTime? UpdatedAt { get; set; }
    }
}
