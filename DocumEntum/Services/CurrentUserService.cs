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

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
            _dbContext = dbContext;
        }
        public async Task<bool> IsSuperAdminAsync()
        {
            return await IsInRoleAsync("SuperAdmin");
        }
        public string? UserId => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        public async Task<bool> IsAdminAsync()
        {
            return await IsInRoleAsync("Admin") || await IsInRoleAsync("SuperAdmin");
        }
        public async Task<Employee?> GetCurrentEmployeeAsync()
        {
            if (_cachedEmployee != null) return _cachedEmployee;
            if (UserId == null) return null;
            // Администраторы не имеют Employee
            if (await IsAdminAsync()) return null;
            _cachedEmployee = await _dbContext.Employees.FirstOrDefaultAsync(e => e.UserId == UserId);
            return _cachedEmployee;
        }

        public async Task<int?> GetEmployeeIdAsync()
        {
            var emp = await GetCurrentEmployeeAsync();
            return emp?.Id;
        }

        public async Task<int?> GetDepartmentIdAsync()
        {
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return null;
            var empPos = await _dbContext.EmployeePositions
                .Where(ep => ep.EmployeeId == emp.Id && ep.EndDate == null)
                .FirstOrDefaultAsync();
            return empPos?.DepartmentId;
        }

        public async Task<bool> IsInRoleAsync(string role)
        {
            if (UserId == null) return false;
            var user = await _userManager.FindByIdAsync(UserId);
            return user != null && await _userManager.IsInRoleAsync(user, role);
        }
    }
}
