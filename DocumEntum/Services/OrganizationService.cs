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
        public async Task<List<Position>> GetAllPositionsWithDetailsAsync()
        {
            return await _dbContext.Positions
                .Include(p => p.Department)
                .Include(p => p.EmployeePositions)
                    .ThenInclude(ep => ep.Employee)
                .ToListAsync();
        }
        public async Task<Department?> GetDepartmentByIdAsync(int id)
        {
            return await _dbContext.Departments.FindAsync(id);
        }
        public async Task<List<Employee>> GetAvailableEmployeesAsync()
        {
            var employeesWithActivePosition = await _dbContext.EmployeePositions
                .Where(ep => ep.EndDate == null)
                .Select(ep => ep.EmployeeId)
                .Distinct()
                .ToListAsync();

            var availableEmployees = await _dbContext.Employees
                .Include(e => e.User)
                .Where(e => !employeesWithActivePosition.Contains(e.Id))
                .ToListAsync();

            return availableEmployees;
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
                .Include(ep => ep.Position)
                .AnyAsync(ep => ep.Position != null && ep.Position.DepartmentId == id && ep.EndDate == null);
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
            var department = await _dbContext.Departments.FindAsync(departmentId);
            if (department == null)
                throw new InvalidOperationException("Указанный отдел не существует.");

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

            var hasActiveEmployees = await _dbContext.EmployeePositions
                .AnyAsync(ep => ep.PositionId == id && ep.EndDate == null);
            if (hasActiveEmployees)
                throw new InvalidOperationException("Нельзя удалить должность, на которую назначены сотрудники.");

            var usedInWorkflow = await _dbContext.WorkflowStates
                .AnyAsync(ws => ws.RequiredPositionId == id);
            if (usedInWorkflow)
                throw new InvalidOperationException("Нельзя удалить должность, которая используется в бизнес-процессе.");

            _dbContext.Positions.Remove(position);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<bool> CanDeletePositionAsync(int id)
        {
            var hasActiveEmployees = await _dbContext.EmployeePositions
                .AnyAsync(ep => ep.PositionId == id && ep.EndDate == null);
            if (hasActiveEmployees) return false;

            var usedInWorkflow = await _dbContext.WorkflowStates
                .AnyAsync(ws => ws.RequiredPositionId == id);
            return !usedInWorkflow;
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

        public async Task AssignEmployeeToPositionAsync(int employeeId, int? positionId, DateTime? startDate = null)
        {
            var employee = await _dbContext.Employees.FindAsync(employeeId);
            if (employee == null)
                throw new InvalidOperationException("Сотрудник не найден.");

            // Если должность не указана — просто закрываем все активные назначения
            if (!positionId.HasValue)
            {
                var active = await _dbContext.EmployeePositions
                    .Where(ep => ep.EmployeeId == employeeId && ep.EndDate == null)
                    .ToListAsync();
                foreach (var a in active)
                    a.EndDate = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                return;
            }

            // Проверяем, не назначен ли уже сотрудник на эту должность активно
            var already = await _dbContext.EmployeePositions
                .AnyAsync(ep => ep.EmployeeId == employeeId && ep.PositionId == positionId && ep.EndDate == null);
            if (already)
                return; // уже занимает эту должность

            // Закрываем текущие активные назначения
            var currentActive = await _dbContext.EmployeePositions
                .Where(ep => ep.EmployeeId == employeeId && ep.EndDate == null)
                .ToListAsync();
            foreach (var active in currentActive)
                active.EndDate = DateTime.UtcNow;

            // Создаём новое назначение
            var position = await _dbContext.Positions.FindAsync(positionId.Value);
            if (position == null)
                throw new InvalidOperationException("Должность не найдена.");

            var employeePosition = new EmployeePosition
            {
                EmployeeId = employeeId,
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
                ep.EndDate = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<List<EmployeePosition>> GetCurrentEmployeePositionsAsync(int employeeId)
        {
            return await _dbContext.EmployeePositions
                .Include(ep => ep.Position)
                    .ThenInclude(p => p.Department)
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