using System.ComponentModel.DataAnnotations;

namespace DocumEntum.Data
{
    /// <summary>
    /// Журнал изменений (аудит) документа.
    /// Фиксирует каждое действие над документом: переход, комментарий, кто и когда выполнил.
    /// </summary>
    public class DocumentHistory
    {
        [Key]
        public int Id { get; set; }
        /// <summary>Идентификатор документа.</summary>
        public int DocumentId { get; set; }
        public virtual Document Document { get; set; } = null!;
        /// <summary>Исходное состояние до перехода (null – для первого действия).</summary>
        public int? FromStateId { get; set; }
        /// <summary>Новое состояние после перехода.</summary>
        public int ToStateId { get; set; }
        /// <summary>Идентификатор сотрудника, выполнившего действие.</summary>
        public int ActionById { get; set; }
        public virtual Employee ActionBy { get; set; } = null!;
        /// <summary>Название выполненного действия (например, "Approve", "Reject").</summary>
        public string ActionName { get; set; } = string.Empty;
        /// <summary>Комментарий, оставленный при переходе.</summary>
        public string? Comment { get; set; }
        /// <summary>Дата и время выполнения действия.</summary>
        public DateTime ActionAt { get; set; } = DateTime.UtcNow;
    }
}
