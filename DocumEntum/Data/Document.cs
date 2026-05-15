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

        // автор
        public int AuthorId { get; set; }
        public virtual Employee Author { get; set; } = null!;
        /// <summary>текущее состояния workflow</summary>
        public int CurrentStateId { get; set; }
        public virtual WorkflowState CurrentState { get; set; } = null!;
        /// <summary>используемый процесс workflow</summary>
        public int WorkflowId { get; set; }
        public virtual Workflow Workflow { get; set; } = null!;
        /// <summary>Идентификатор отдела, к которому привязан документ (может быть null)</summary>
        public int? DepartmentId { get; set; }
        public virtual Department? Department { get; set; }


        public int DocumentTypeId { get; set; }
        public virtual DocumentType DocumentType { get; set; } = null!;

        /// <summary>
        /// Если задано — этот экземпляр проходит workflow как изменение указанного утверждённого документа.
        /// </summary>
        public int? ReplacesDocumentId { get; set; }
        public virtual Document? ReplacedDocument { get; set; }

        /// <summary>
        /// Номер версии для утверждённого документа (1 после первого утверждения; у черновика в процессе — 0).
        /// </summary>
        public int ApprovedVersion { get; set; }

        /// <summary>
        /// Помечен ли документ как удалённый (soft delete).
        /// Для утверждённых документов при скрытии запись и файл остаются; для черновиков в процессе удаление иное правило.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Динамические атрибуты документа в формате JSONB
        /// Позволяет хранить произвольную структуру полей
        /// В дааной реализации не используется (предусмотренно как возможность модификации без изменения структуры БД)
        /// </summary> 
        [Column(TypeName = "jsonb")]
        public Dictionary<string, object> ExtraAttributes { get; set; } = new();
        /// <summary>Дата создания документа.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>Дата последнего обновления.</summary>
        public DateTime? UpdatedAt { get; set; }
    }
}
