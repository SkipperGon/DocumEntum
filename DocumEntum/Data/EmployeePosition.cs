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
        /// <summary>Идентификатор сотрудника.</summary>
        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; } = null!;
        /// <summary>Идентификатор отдела.</summary>
        public int DepartmentId { get; set; }
        public virtual Department Department { get; set; } = null!;
        /// <summary>Идентификатор должности.</summary>
        public int PositionId { get; set; }
        public virtual Position Position { get; set; } = null!;
        /// <summary>Дата начала назначения.</summary>
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        /// <summary>Дата окончания назначения (null – действующее).</summary>
        public DateTime? EndDate { get; set; }
    }
}
