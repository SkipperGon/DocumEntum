using DocumEntum.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumEntum.Services
{

    public class WorkflowService : IWorkflowService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly IFileStorageService _fileStorage;

        public WorkflowService(ApplicationDbContext dbContext, ICurrentUserService currentUserService, IFileStorageService fileStorage)
        {
            _dbContext = dbContext;
            _currentUserService = currentUserService;
            _fileStorage= fileStorage;
        }
        public async Task<bool> CanEditDocumentAsync(Document document, int employeeId)
        {
            // администраторы не редактируют документы
            if (await _currentUserService.IsAdminAsync())
                return false;

            var currentState = await _dbContext.WorkflowStates
                .Include(s => s.RequiredPosition)
                .FirstOrDefaultAsync(s => s.Id == document.CurrentStateId);
            if (currentState == null) return false;

            // редактирование запрещено
            if (!currentState.CanEdit) return false;

            // Если этап не привязан к должности – редактировать может любой сотрудник (осторожно)
            if (currentState.RequiredPositionId == null)
                return true; // или false – зависит от политики безопасности

            // Проверяем, что текущий сотрудник занимает требуемую должность
            var employeePositions = await _dbContext.EmployeePositions
                .Where(ep => ep.EmployeeId == employeeId && ep.EndDate == null && ep.PositionId != null)
                .Select(ep => ep.PositionId.Value)
                .ToListAsync();

            return employeePositions.Contains(currentState.RequiredPositionId.Value);
        }
        public async Task<List<WorkflowTransition>> GetAvailableTransitionsAsync(Document document, int employeeId)
        {
            // админы не участвуют в workflow
            if (await _currentUserService.IsAdminAsync())
                return new List<WorkflowTransition>();

            var currentState = await _dbContext.WorkflowStates
                .FirstOrDefaultAsync(s => s.Id == document.CurrentStateId);
            if (currentState == null)
                return new List<WorkflowTransition>();

            // Проверка на право работать с этапом (требуемая должность)
            if (currentState.RequiredPositionId != null)
            {
                var employeePositions = await _dbContext.EmployeePositions
                    .Where(ep => ep.EmployeeId == employeeId && ep.EndDate == null && ep.PositionId != null)
                    .Select(ep => ep.PositionId.Value)
                    .ToListAsync();

                if (!employeePositions.Contains(currentState.RequiredPositionId.Value))
                    return new List<WorkflowTransition>();
            }

            var employeePositionIds = await _dbContext.EmployeePositions
                .Where(ep => ep.EmployeeId == employeeId && ep.EndDate == null && ep.PositionId != null)
                .Select(ep => ep.PositionId.Value)
                .ToListAsync();

            // Загружаем все возможные переходы из текущего состояния
            var transitions = await _dbContext.WorkflowTransitions
                .Where(t => t.WorkflowId == document.WorkflowId && t.FromStateId == document.CurrentStateId)
                .ToListAsync();

            // Фильтруем только по должностям (AllowedRoles больше нет)
            var available = transitions.Where(t =>
            {
                var positionsOk = string.IsNullOrEmpty(t.AllowedPositionIds) ||
                                  t.AllowedPositionIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(p => int.Parse(p.Trim()))
                                      .Any(posId => employeePositionIds.Contains(posId));

                return positionsOk;
            }).ToList();

            return available;
        }

        private async Task<List<string>> GetUserRolesAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return new List<string>();

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return new List<string>();

            var userRoles = await (from ur in _dbContext.UserRoles
                                   join r in _dbContext.Roles on ur.RoleId equals r.Id
                                   where ur.UserId == userId
                                   select r.Name ?? "")
                                  .ToListAsync();
            return userRoles;
        }
        public async Task<bool> ExecuteTransitionAsync(Document document, string actionName, int employeeId, string? comment = null)
        {
            if (await _currentUserService.IsAdminAsync())
                return false;

            var transition = await _dbContext.WorkflowTransitions
                .FirstOrDefaultAsync(t => t.WorkflowId == document.WorkflowId
                                          && t.FromStateId == document.CurrentStateId
                                          && t.ActionName == actionName);
            if (transition == null) return false;

            var available = await GetAvailableTransitionsAsync(document, employeeId);
            if (!available.Any(t => t.Id == transition.Id)) return false;

            var fromStateId = document.CurrentStateId;
            document.CurrentStateId = transition.ToStateId;
            document.UpdatedAt = DateTime.UtcNow;

            // Получаем целевое состояние
            var newState = await _dbContext.WorkflowStates.FindAsync(transition.ToStateId);
            if (newState != null)
            {
                if (newState.IsFinal && !newState.IsRejected)
                {
                    // Утверждённый документ: перемещаем файл из pending в корневую папку
                    if (!string.IsNullOrEmpty(document.StoredFileName) &&
                        document.StoredFileName.StartsWith("pending/"))
                    {
                        var newPath = await _fileStorage.MoveFileAsync(document.StoredFileName, "");
                        document.StoredFileName = newPath;
                    }
                }
                else if (newState.IsRejected)
                {
                    // Окончательная браковка: удаляем файл и помечаем документ как удалённый
                    if (!string.IsNullOrEmpty(document.StoredFileName))
                        await _fileStorage.DeleteFileAsync(document.StoredFileName);
                    document.StoredFileName = null;
                    document.IsDeleted = true;
                }
            }

            _dbContext.DocumentHistories.Add(new DocumentHistory
            {
                DocumentId = document.Id,
                FromStateId = fromStateId,
                ToStateId = transition.ToStateId,
                ActionById = employeeId,
                ActionName = actionName,
                Comment = comment,
                ActionAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
            return true;
        }

       
    }
}
