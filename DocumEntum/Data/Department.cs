using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumEntum.Data
{
    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public int? ParentId { get; set; }

        [ForeignKey(nameof(ParentId))]
        public virtual Department? Parent { get; set; }

        public virtual ICollection<Department> Children { get; set; } = new List<Department>();

        // PostgreSQL ltree path для быстрых иерархических запросов
        public string? Path { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
