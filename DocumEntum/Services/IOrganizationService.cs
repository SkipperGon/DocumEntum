using DocumEntum.Data;

namespace DocumEntum.Services
{
    public interface IOrganizationService
    {
        // Отделы
        Task<List<Department>> GetAllDepartmentsAsync();
        Task<List<Department>> GetAllDepartmentsFlatAsync();
        Task<Department?> GetDepartmentByIdAsync(int id);
        Task<Department> CreateDepartmentAsync(string name, int? parentId);
        Task UpdateDepartmentAsync(Department department);
        Task DeleteDepartmentAsync(int id);
        Task<bool> CanDeleteDepartmentAsync(int id);

        // Должности 
        Task<List<Position>> GetPositionsByDepartmentAsync(int departmentId);
        Task<Position?> GetPositionByIdAsync(int id);
        Task<Position> CreatePositionAsync(int departmentId, string title, string? description = null);
        Task UpdatePositionAsync(Position position);
        Task DeletePositionAsync(int id);
        // проверка наличия сотрудников на должности
        Task<bool> CanDeletePositionAsync(int id);
        Task<List<Position>> GetAllPositionsWithDetailsAsync();

        // Сотрудники
        Task<List<Employee>> GetAllEmployeesAsync();
        Task<Employee?> GetEmployeeByUserIdAsync(string userId);
        Task AssignEmployeeToPositionAsync(int employeeId, int? positionId, DateTime? startDate = null);
        Task RemoveEmployeeFromPositionAsync(int employeePositionId);
        // действующие назначения
        Task<List<EmployeePosition>> GetCurrentEmployeePositionsAsync(int employeeId);
        // кто занимает должность
        Task<List<EmployeePosition>> GetEmployeesByPositionAsync(int positionId);
        Task<List<Employee>> GetAvailableEmployeesAsync();

    }
}
