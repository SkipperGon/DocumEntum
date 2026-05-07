using DocumEntum.Data;

namespace DocumEntum.Services
{
    public interface IOrganizationService
    {
        Task<List<Department>> GetAllDepartmentsAsync();
        Task<Department> CreateDepartmentAsync(string name, int? parentId);
        Task DeleteDepartmentAsync(int id);
        Task<List<Employee>> GetAllEmployeesAsync();
        Task AssignEmployeeToPositionAsync(int employeeId, int departmentId, int positionId);

        Task<Department?> GetDepartmentByIdAsync(int id);
        Task UpdateDepartmentAsync(Department department);
        Task<bool> CanDeleteDepartmentAsync(int id);
        Task<List<Department>> GetAllDepartmentsFlatAsync();
    }
}
