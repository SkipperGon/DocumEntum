using System.Security.Claims;
using DocumEntum.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DocumEntum.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;
        private Employee? _cachedEmployee;
        private List<EmployeePosition>? _cachedPositions;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
            _dbContext = dbContext;
        }

        public string? UserId => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        public async Task<bool> IsSuperAdminAsync() => await IsInRoleAsync("SuperAdmin");
        public async Task<bool> IsAdminAsync() => await IsInRoleAsync("Admin") || await IsInRoleAsync("SuperAdmin");

        public async Task<Employee?> GetCurrentEmployeeAsync()
        {
            if (_cachedEmployee != null) return _cachedEmployee;
            if (UserId == null) return null;
            if (await IsAdminAsync()) return null;
            _cachedEmployee = await _dbContext.Employees.FirstOrDefaultAsync(e => e.UserId == UserId);
            return _cachedEmployee;
        }

        public async Task<int?> GetEmployeeIdAsync()
        {
            var emp = await GetCurrentEmployeeAsync();
            return emp?.Id;
        }

        /// <summary>
        /// Возвращает ID отдела, в котором сотрудник работает по основной должности.
        /// Если у сотрудника несколько должностей – возвращает первый попавшийся (можно уточнить логику).
        /// </summary>
        public async Task<int?> GetDepartmentIdAsync()
        {
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return null;
            var currentPosition = await _dbContext.EmployeePositions
                .Where(ep => ep.EmployeeId == emp.Id && ep.EndDate == null)
                .FirstOrDefaultAsync();
            return currentPosition?.DepartmentId;
        }

        /// <summary>
        /// Возвращает список всех действующих должностей сотрудника.
        /// </summary>
        public async Task<List<EmployeePosition>> GetCurrentPositionsAsync()
        {
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return new List<EmployeePosition>();
            if (_cachedPositions != null) return _cachedPositions;
            _cachedPositions = await _dbContext.EmployeePositions
                .Include(ep => ep.Department)
                .Include(ep => ep.Position)
                .Where(ep => ep.EmployeeId == emp.Id && ep.EndDate == null)
                .ToListAsync();
            return _cachedPositions;
        }

        public async Task<bool> IsInRoleAsync(string role)
        {
            if (UserId == null) return false;
            var user = await _userManager.FindByIdAsync(UserId);
            return user != null && await _userManager.IsInRoleAsync(user, role);
        }
    }
}