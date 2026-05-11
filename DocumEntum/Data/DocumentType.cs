using System.ComponentModel.DataAnnotations;

namespace DocumEntum.Data
{
    public class DocumentType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        // Отделы, которым разрешён просмотр документов этого типа в утверждённом состоянии
        public virtual ICollection<Department> AvailableDepartments { get; set; } = new List<Department>();

        public virtual ICollection<Workflow> Workflows { get; set; } = new List<Workflow>();
    }
}