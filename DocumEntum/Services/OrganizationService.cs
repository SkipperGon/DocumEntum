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

        // Отделы
        public async Task<List<Department>> GetAllDepartmentsAsync()
        {
            return await _dbContext.Departments
                .Include(d => d.Children)
                .ToListAsync();
        }

        public async Task<List<Department>> GetAllDepartmentsFlatAsync()
        {
            return await _dbContext.Departments
                .OrderBy(d => d.Path)
                .ThenBy(d => d.Id)
                .ToListAsync();
        }

        public async Task<Department?> GetDepartmentByIdAsync(int id)
        {
            return await _dbContext.Departments.FindAsync(id);
        }

        public async Task<Department> CreateDepartmentAsync(string name, int? parentId)
        {
            var dept = new Department { Name = name, ParentId = parentId };
            if (parentId == null)
                dept.Path = $"{dept.Id}";
            else
            {
                var parent = await _dbContext.Departments.FindAsync(parentId);
                dept.Path = parent?.Path != null ? $"{parent.Path}.{dept.Id}" : $"{dept.Id}";
            }
            _dbContext.Departments.Add(dept);
            await _dbContext.SaveChangesAsync();
            return dept;
        }

        public async Task UpdateDepartmentAsync(Department department)
        {
            _dbContext.Departments.Update(department);
            await RecalculatePathAsync(department);
            await _dbContext.SaveChangesAsync();
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

        public async Task<bool> CanDeleteDepartmentAsync(int id)
        {
            var hasChildren = await _dbContext.Departments.AnyAsync(d => d.ParentId == id);
            if (hasChildren) return false;
            var hasDocuments = await _dbContext.Documents.AnyAsync(d => d.DepartmentId == id);
            if (hasDocuments) return false;
            // Проверяем есть ли активные назначения сотрудников на должности этого отдела
            var hasActiveEmployees = await _dbContext.EmployeePositions
                .AnyAsync(ep => ep.DepartmentId == id && ep.EndDate == null);
            return !hasActiveEmployees;
        }

        private async Task RecalculatePathAsync(Department department)
        {
            if (department.ParentId == null)
                department.Path = department.Id.ToString();
            else
            {
                var parent = await _dbContext.Departments.FindAsync(department.ParentId);
                department.Path = parent?.Path != null ? $"{parent.Path}.{department.Id}" : department.Id.ToString();
            }
        }

        // Должности
        public async Task<List<Position>> GetPositionsByDepartmentAsync(int departmentId)
        {
            return await _dbContext.Positions
                .Where(p => p.DepartmentId == departmentId)
                .OrderBy(p => p.Title)
                .ToListAsync();
        }

        public async Task<Position?> GetPositionByIdAsync(int id)
        {
            return await _dbContext.Positions
                .Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Position> CreatePositionAsync(int departmentId, string title, string? description = null)
        {
            // Проверяем отдел на существование 
            var department = await _dbContext.Departments.FindAsync(departmentId);
            if (department == null)
                throw new InvalidOperationException("Указанный отдел не существует.");

            // Проверяем уникальность названия должности внутри отдела
            var exists = await _dbContext.Positions
                .AnyAsync(p => p.DepartmentId == departmentId && p.Title == title);
            if (exists)
                throw new InvalidOperationException("Должность с таким названием уже существует в этом отделе.");

            var position = new Position
            {
                Title = title,
                Description = description,
                DepartmentId = departmentId
            };
            _dbContext.Positions.Add(position);
            await _dbContext.SaveChangesAsync();
            return position;
        }

        public async Task UpdatePositionAsync(Position position)
        {
            // Проверяем название новой должности конфликт по названию внутри отдела
            var conflict = await _dbContext.Positions
                .AnyAsync(p => p.DepartmentId == position.DepartmentId && p.Title == position.Title && p.Id != position.Id);
            if (conflict)
                throw new InvalidOperationException("Должность с таким названием уже существует в этом отделе.");

            _dbContext.Positions.Update(position);
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeletePositionAsync(int id)
        {
            var position = await _dbContext.Positions.FindAsync(id);
            if (position == null) return;

            // Проверяем активные назначения сотрудников на эту должность
            var hasActiveEmployees = await _dbContext.EmployeePositions
                .AnyAsync(ep => ep.PositionId == id && ep.EndDate == null);
            if (hasActiveEmployees)
                throw new InvalidOperationException("Нельзя удалить должность, на которую назначены сотрудники.");

            _dbContext.Positions.Remove(position);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<bool> CanDeletePositionAsync(int id)
        {
            // Проверяем на активные назначения
            var hasActiveEmployees = await _dbContext.EmployeePositions
                .AnyAsync(ep => ep.PositionId == id && ep.EndDate == null);
            return !hasActiveEmployees;
        }

        // Сотрудники
        public async Task<List<Employee>> GetAllEmployeesAsync()
        {
            return await _dbContext.Employees.ToListAsync();
        }

        public async Task<Employee?> GetEmployeeByUserIdAsync(string userId)
        {
            return await _dbContext.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
        }
        public async Task AssignEmployeeToPositionAsync(int employeeId, int departmentId, int? positionId, DateTime? startDate = null)
        {
            // Проверяем существование сотрудника
            var employee = await _dbContext.Employees.FindAsync(employeeId);
            if (employee == null)
                throw new InvalidOperationException("Сотрудник не найден.");

            // Проверяем существование отдела
            var department = await _dbContext.Departments.FindAsync(departmentId);
            if (department == null)
                throw new InvalidOperationException("Отдел не найден.");

            // Если указана должность, проверяем её принадлежность отделу
            if (positionId.HasValue)
            {
                var position = await _dbContext.Positions
                    .FirstOrDefaultAsync(p => p.Id == positionId && p.DepartmentId == departmentId);
                if (position == null)
                    throw new InvalidOperationException("Должность не принадлежит выбранному отделу.");
            }

            // Проверяем, нет ли уже активного назначения на ту же должность (или без должности)
            var existing = await _dbContext.EmployeePositions
                .FirstOrDefaultAsync(ep => ep.EmployeeId == employeeId &&
                                           ep.DepartmentId == departmentId &&
                                           ep.PositionId == positionId &&
                                           ep.EndDate == null);
            if (existing != null)
                throw new InvalidOperationException("Сотрудник уже имеет активное назначение на эту же должность (или без должности) в данном отделе.");

            var employeePosition = new EmployeePosition
            {
                EmployeeId = employeeId,
                DepartmentId = departmentId,
                PositionId = positionId,
                StartDate = startDate ?? DateTime.UtcNow,
                EndDate = null
            };
            _dbContext.EmployeePositions.Add(employeePosition);
            await _dbContext.SaveChangesAsync();
        }

        public async Task RemoveEmployeeFromPositionAsync(int employeePositionId)
        {
            var ep = await _dbContext.EmployeePositions.FindAsync(employeePositionId);
            if (ep != null)
            {
                // Мягкое удаление через дату окончания
                ep.EndDate = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<List<EmployeePosition>> GetCurrentEmployeePositionsAsync(int employeeId)
        {
            return await _dbContext.EmployeePositions
                .Include(ep => ep.Department)
                .Include(ep => ep.Position)
                .Where(ep => ep.EmployeeId == employeeId && ep.EndDate == null)
                .ToListAsync();
        }

        public async Task<List<EmployeePosition>> GetEmployeesByPositionAsync(int positionId)
        {
            return await _dbContext.EmployeePositions
                .Include(ep => ep.Employee)
                .Where(ep => ep.PositionId == positionId && ep.EndDate == null)
                .ToListAsync();
        }
    }
}