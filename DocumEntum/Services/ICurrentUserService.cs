using DocumEntum.Data;

namespace DocumEntum.Services
{

    public interface ICurrentUserService
    {
        string? UserId { get; }
        Task<int?> GetEmployeeIdAsync();
        Task<int?> GetDepartmentIdAsync();
        Task<bool> IsInRoleAsync(string role);
        Task<Employee?> GetCurrentEmployeeAsync();
    }
}
