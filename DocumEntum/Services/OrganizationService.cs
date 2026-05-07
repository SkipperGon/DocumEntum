using DocumEntum.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumEntum.Services
{

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
        public async Task<Department?> GetDepartmentByIdAsync(int id)
        {
            return await _dbContext.Departments.FindAsync(id);
        }

        public async Task UpdateDepartmentAsync(Department department)
        {
            _dbContext.Departments.Update(department);
            // Если изменился родитель – пересчитываем Path (опционально, но рекомендуется)
            await RecalculatePathAsync(department);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<bool> CanDeleteDepartmentAsync(int id)
        {
            var hasChildren = await _dbContext.Departments.AnyAsync(d => d.ParentId == id);
            if (hasChildren) return false;
            var hasDocuments = await _dbContext.Documents.AnyAsync(d => d.DepartmentId == id);
            return !hasDocuments;
        }

        public async Task<List<Department>> GetAllDepartmentsFlatAsync()
        {
            return await _dbContext.Departments
                .OrderBy(d => d.Path)
                .ThenBy(d => d.Id)
                .ToListAsync();
        }

        private async Task RecalculatePathAsync(Department department)
        {
            if (department.ParentId == null)
                department.Path = department.Id.ToString();
            else
            {
                var parent = await _dbContext.Departments.FindAsync(department.ParentId);
                department.Path = parent?.Path + "." + department.Id;
            }
        }
    }
}
