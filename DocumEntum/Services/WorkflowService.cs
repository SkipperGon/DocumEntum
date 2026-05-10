using DocumEntum.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumEntum.Services
{
    public interface IWorkflowService
    {
        Task<List<WorkflowTransition>> GetAvailableTransitionsAsync(Document document, int employeeId);
        Task<bool> ExecuteTransitionAsync(Document document, string actionName, int employeeId, string? comment = null);
    }

    public class WorkflowService : IWorkflowService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;

        public WorkflowService(ApplicationDbContext dbContext, ICurrentUserService currentUserService)
        {
            _dbContext = dbContext;
            _currentUserService = currentUserService;
        }

        public async Task<List<WorkflowTransition>> GetAvailableTransitionsAsync(Document document, int employeeId)
        {
            // Администратор не может выполнять переходы
            if (await _currentUserService.IsAdminAsync())
                return new List<WorkflowTransition>();

            var employee = await _dbContext.Employees.FindAsync(employeeId);
            if (employee == null) return new List<WorkflowTransition>();

            var userRoles = await GetUserRolesAsync(employee.UserId);
            var employeePositions = await _dbContext.EmployeePositions
    .Where(ep => ep.EmployeeId == employeeId && ep.EndDate == null && ep.PositionId != null)
    .Select(ep => ep.PositionId.Value)
    .ToListAsync();

            var transitions = await _dbContext.WorkflowTransitions
                .Where(t => t.WorkflowId == document.WorkflowId && t.FromStateId == document.CurrentStateId)
                .ToListAsync();

            var available = transitions.Where(t =>
            {
                var rolesOk = string.IsNullOrEmpty(t.AllowedRoles) ||
                              t.AllowedRoles.Split(',').Any(r => userRoles.Contains(r.Trim()));
                var positionsOk = string.IsNullOrEmpty(t.AllowedPositionIds) ||
                                  t.AllowedPositionIds.Split(',').Select(int.Parse).Any(id => employeePositions.Contains(id));
                return rolesOk || positionsOk;
            }).ToList();

            return available;
        }

        public async Task<bool> ExecuteTransitionAsync(Document document, string actionName, int employeeId, string? comment = null)
        {
            if (await _currentUserService.IsAdminAsync())
                return false;

            var transition = await _dbContext.WorkflowTransitions
                .FirstOrDefaultAsync(t => t.WorkflowId == document.WorkflowId && t.FromStateId == document.CurrentStateId && t.ActionName == actionName);

            if (transition == null) return false;

            var available = await GetAvailableTransitionsAsync(document, employeeId);
            if (!available.Any(t => t.Id == transition.Id)) return false;

            var fromStateId = document.CurrentStateId;
            document.CurrentStateId = transition.ToStateId;
            document.UpdatedAt = DateTime.UtcNow;

            _dbContext.DocumentHistories.Add(new DocumentHistory
            {
                DocumentId = document.Id,
                FromStateId = fromStateId,
                ToStateId = transition.ToStateId,
                ActionById = employeeId,
                ActionName = actionName,
                Comment = comment
            });

            await _dbContext.SaveChangesAsync();
            return true;
        }

        private async Task<List<string>> GetUserRolesAsync(string userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return new List<string>();
            var roles = await _dbContext.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync();
            var roleNames = await _dbContext.Roles.Where(r => roles.Contains(r.Id)).Select(r => r.Name).ToListAsync();
            return roleNames!;
        }
    }
}
