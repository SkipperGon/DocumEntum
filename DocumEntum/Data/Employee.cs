using System.ComponentModel.DataAnnotations;

namespace DocumEntum.Data
{
    /// <summary>
    /// Сотрудник. Связан с учётной записью ApplicationUser.
    /// Содержит основную информацию о физическом лице.
    /// </summary>
    public class Employee
    {
        [Key]
        public int Id { get; set; }
        /// <summary>Внешний ключ на ApplicationUser (Identity).</summary>
        [Required]
        public string UserId { get; set; } = string.Empty;
        /// <summary>Навигационное свойство: связанный пользователь Identity.</summary>
        public virtual ApplicationUser User { get; set; } = null!;
        /// <summary>Полное имя сотрудника.</summary>
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        public string? Email { get; set; }
    }
}
