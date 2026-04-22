using System.ComponentModel.DataAnnotations;

namespace DocumEntum.Data
{
    public class Employee
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public virtual ApplicationUser User { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        public string? Email { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
