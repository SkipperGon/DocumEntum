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


    }
}
