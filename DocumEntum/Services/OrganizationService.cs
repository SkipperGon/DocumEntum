using DocumEntum.Data;
using Microsoft.EntityFrameworkCore;

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

    public class OrganizationService : IOrganizationService
    {
        private readonly ApplicationDbContext _dbContext;

        public OrganizationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Department>> GetAllDepartmentsAsync()
        {
            return await _dbContext.Departments
                .Include(d => d.Children)
                .ToListAsync();
        }

        public async Task<Department> CreateDepartmentAsync(string name, int? parentId)
        {
            var dept = new Department { Name = name, ParentId = parentId };
            // Генерация ltree пути (упрощённо)
            if (parentId == null)
                dept.Path = $"{dept.Id}";
            else
            {
                var parent = await _dbContext.Departments.FindAsync(parentId);
                dept.Path = $"{parent?.Path}.{dept.Id}";
            }
            _dbContext.Departments.Add(dept);
            await _dbContext.SaveChangesAsync();
            return dept;
        }

        public async Task DeleteDepartmentAsync(int id)
        {
            var dept = await _dbContext.Departments.FindAsync(id);
            if (dept != null)
            {
                _dbContext.Departments.Remove(dept);
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<List<Employee>> GetAllEmployeesAsync()
        {
            return await _dbContext.Employees.ToListAsync();
        }

        public async Task AssignEmployeeToPositionAsync(int employeeId, int departmentId, int positionId)
        {
            var empPos = new EmployeePosition
            {
                EmployeeId = employeeId,
                DepartmentId = departmentId,
                PositionId = positionId
            };
            _dbContext.EmployeePositions.Add(empPos);
            await _dbContext.SaveChangesAsync();
        }
    }
}
