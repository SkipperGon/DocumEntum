using System.ComponentModel.DataAnnotations;

namespace DocumEntum.Data
{
    /// <summary>
    /// Назначение сотрудника на должность в конкретном отделе.
    /// Позволяет одному сотруднику занимать несколько должностей в разных отделах.
    /// </summary>
    public class EmployeePosition
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Сотрудник</summary>
        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; } = null!;

        /// <summary>Должностб</summary>
        public int? PositionId { get; set; }
        public virtual Position? Position { get; set; } = null!;

        /// <summary>Дата начала назначения</summary>
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        /// <summary>Дата окончания назначения(null–действующее)</summary>
        public DateTime? EndDate { get; set; }
    }

}
