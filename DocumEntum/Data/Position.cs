using System.ComponentModel.DataAnnotations;

namespace DocumEntum.Data
{
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
