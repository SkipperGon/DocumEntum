using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumEntum.Data
{
    /// <summary>
    /// Отдел организации. Поддерживает рекурсивную иерархию (родитель-потомок).
    /// Поле Path (ltree) позволяет эффективно выполнять иерархические запросы в PostgreSQL.
    /// </summary>
    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        /// <summary>Идентификатор родительского отдела (null для корневого).</summary>
        public int? ParentId { get; set; }
        /// <summary>Родительский отдел (навигационное свойство).</summary>

        [ForeignKey(nameof(ParentId))]
        public virtual Department? Parent { get; set; }
        /// <summary>Коллекция дочерних отделов.</summary>
        public virtual ICollection<Department> Children { get; set; } = new List<Department>();
        /// <summary>Коллекция его должностефй.</summary>
        public virtual ICollection<Position> Positions { get; set; } = new List<Position>();

        // PostgreSQL ltree path для быстрых иерархических запросов
        [Column(TypeName = "ltree")]
        public string? Path { get; set; }
        /// <summary>Дата создания отдела.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
