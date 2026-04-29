using System.ComponentModel.DataAnnotations;

namespace DocumEntum.Data
{
    /// <summary>
    /// Справочник должностей (например, "Бухгалтер", "Начальник отдела").
    /// </summary>
    public class Position
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
