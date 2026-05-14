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
            _fileStorage = fileStorage;
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
                .Select(ep => ep.PositionId!.Value)
                .ToListAsync();

            return employeePositions.Contains(currentState.RequiredPositionId.Value);
        }
        public async Task<List<WorkflowTransition>> GetAvailableTransitionsAsync(Document document, int employeeId)
        {
            if (await _currentUserService.IsAdminAsync())
                return new List<WorkflowTransition>();

            var currentState = await _dbContext.WorkflowStates
                .FirstOrDefaultAsync(s => s.Id == document.CurrentStateId);
            if (currentState == null)
                return new List<WorkflowTransition>();

            var employeePositionIds = await _dbContext.EmployeePositions
                .Where(ep => ep.EmployeeId == employeeId && ep.EndDate == null && ep.PositionId != null)
                .Select(ep => ep.PositionId!.Value)
                .ToListAsync();

            // Является ли текущий сотрудник владельцем данного этапа
            bool isStateOwner = currentState.RequiredPositionId == null || employeePositionIds.Contains(currentState.RequiredPositionId.Value);

            var transitions = await _dbContext.WorkflowTransitions
                .Include(t => t.ToState)
                .Where(t => t.WorkflowId == document.WorkflowId && t.FromStateId == document.CurrentStateId)
                .ToListAsync();

            var available = transitions.Where(t =>
            {
                // Если для перехода явно указаны должности, проверяем их (это переопределяет владение состоянием)
                if (!string.IsNullOrEmpty(t.AllowedPositionIds))
                {
                    var allowedIds = t.AllowedPositionIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => int.Parse(p.Trim()));
                    return allowedIds.Any(posId => employeePositionIds.Contains(posId));
                }
                // Иначе переход доступен только владельцу текущего состояния
                return isStateOwner;
            }).ToList();

            return available;
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

            if (document.ReplacesDocumentId is int replacedId)
            {
                var canonical = await _dbContext.Documents
                    .Include(d => d.CurrentState)
                    .FirstOrDefaultAsync(d => d.Id == replacedId);
                if (canonical == null || canonical.CurrentState == null || !canonical.CurrentState.IsFinal || canonical.IsDeleted)
                    return false;
            }

            var fromStateId = document.CurrentStateId;
            document.CurrentStateId = transition.ToStateId;
            document.UpdatedAt = DateTime.UtcNow;

            var newState = await _dbContext.WorkflowStates.FindAsync(transition.ToStateId);
            if (newState == null)
                return false;

            if (newState.IsRejected)
            {
                await RejectWorkflowDocumentAsync(document);
                await _dbContext.SaveChangesAsync();
                return true;
            }

            if (newState.IsFinal && document.ReplacesDocumentId is int canonicalId)
            {
                await MergeDraftIntoCanonicalAsync(document, canonicalId, fromStateId, transition.ToStateId, actionName, employeeId, comment);
                await _dbContext.SaveChangesAsync();
                return true;
            }

            if (newState.IsFinal)
            {
                await CompleteFirstApprovalAsync(document);
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

        private async Task CompleteFirstApprovalAsync(Document document)
        {
            if (!string.IsNullOrEmpty(document.StoredFileName) &&
                DocumentStorageFolders.IsStagingWorkflowPath(document.StoredFileName))
            {
                var newPath = await _fileStorage.MoveFileAsync(document.StoredFileName, DocumentStorageFolders.Documents);
                document.StoredFileName = newPath;
            }

            document.ApprovedVersion = 1;
        }

        private async Task MergeDraftIntoCanonicalAsync(
            Document draft,
            int canonicalId,
            int fromStateId,
            int toStateId,
            string actionName,
            int employeeId,
            string? comment)
        {
            var canonical = await _dbContext.Documents
                .FirstAsync(d => d.Id == canonicalId);

            var previousVersion = Math.Max(canonical.ApprovedVersion, 1);

            _dbContext.DocumentVersions.Add(new DocumentVersion
            {
                DocumentId = canonical.Id,
                VersionNumber = previousVersion,
                Title = canonical.Title,
                FileName = canonical.FileName,
                StoredFileName = canonical.StoredFileName,
                FileExtension = canonical.FileExtension,
                FileSize = canonical.FileSize,
                ContentType = canonical.ContentType,
                ExtraAttributes = CloneExtraAttributes(canonical.ExtraAttributes),
                ArchivedAt = DateTime.UtcNow
            });

            string newStoredPath;
            if (!string.IsNullOrEmpty(draft.StoredFileName) &&
                DocumentStorageFolders.IsStagingWorkflowPath(draft.StoredFileName))
            {
                newStoredPath = await _fileStorage.MoveFileAsync(draft.StoredFileName, DocumentStorageFolders.Documents);
            }
            else if (!string.IsNullOrEmpty(draft.StoredFileName))
            {
                newStoredPath = await _fileStorage.CopyFileAsync(draft.StoredFileName, DocumentStorageFolders.Documents);
            }
            else
            {
                newStoredPath = canonical.StoredFileName;
            }

            canonical.Title = draft.Title;
            canonical.FileName = draft.FileName;
            canonical.StoredFileName = newStoredPath;
            canonical.FileExtension = draft.FileExtension;
            canonical.FileSize = draft.FileSize;
            canonical.ContentType = draft.ContentType;
            canonical.ExtraAttributes = CloneExtraAttributes(draft.ExtraAttributes);
            canonical.UpdatedAt = DateTime.UtcNow;
            canonical.ApprovedVersion = previousVersion + 1;
            if (draft.DepartmentId.HasValue)
                canonical.DepartmentId = draft.DepartmentId;

            var draftHistories = await _dbContext.DocumentHistories.Where(h => h.DocumentId == draft.Id).ToListAsync();
            foreach (var h in draftHistories)
                h.DocumentId = canonical.Id;

            _dbContext.DocumentHistories.Add(new DocumentHistory
            {
                DocumentId = canonical.Id,
                FromStateId = fromStateId,
                ToStateId = toStateId,
                ActionById = employeeId,
                ActionName = actionName,
                Comment = comment,
                ActionAt = DateTime.UtcNow
            });

            _dbContext.Documents.Remove(draft);
        }

        private async Task RejectWorkflowDocumentAsync(Document document)
        {
            if (!string.IsNullOrEmpty(document.StoredFileName))
                await _fileStorage.DeleteFileAsync(document.StoredFileName);

            var histories = await _dbContext.DocumentHistories.Where(h => h.DocumentId == document.Id).ToListAsync();
            _dbContext.DocumentHistories.RemoveRange(histories);
            _dbContext.Documents.Remove(document);
        }

        public async Task<List<Workflow>> GetStartableWorkflowsAsync()
        {
            var employee = await _currentUserService.GetCurrentEmployeeAsync();
            if (employee == null)
                return new List<Workflow>();

            var workflowIds = await (
                from s in _dbContext.WorkflowStates
                join w in _dbContext.Workflows on s.WorkflowId equals w.Id
                where s.IsInitial && w.IsActive
                      && (s.RequiredPositionId == null
                          || _dbContext.EmployeePositions.Any(ep =>
                              ep.EmployeeId == employee.Id
                              && ep.EndDate == null
                              && ep.PositionId != null
                              && ep.PositionId == s.RequiredPositionId))
                select w.Id).Distinct().ToListAsync();

            return await _dbContext.Workflows
                .Include(w => w.DocumentType)
                .Where(w => workflowIds.Contains(w.Id))
                .OrderBy(w => w.Name)
                .ToListAsync();
        }

        public async Task<bool> CanEmployeeStartWorkflowAsync(int employeeId, int workflowId)
        {
            return await (
                from s in _dbContext.WorkflowStates
                join w in _dbContext.Workflows on s.WorkflowId equals w.Id
                where w.Id == workflowId && w.IsActive && s.IsInitial
                      && (s.RequiredPositionId == null
                          || _dbContext.EmployeePositions.Any(ep =>
                              ep.EmployeeId == employeeId
                              && ep.EndDate == null
                              && ep.PositionId != null
                              && ep.PositionId == s.RequiredPositionId))
                select s).AnyAsync();
        }

        public async Task<bool> CanEmployeeAccessWorkflowDocumentAsync(Document document, int employeeId)
        {
            if (document.AuthorId == employeeId)
                return true;

            var state = document.CurrentState;
            if (state == null)
            {
                state = await _dbContext.WorkflowStates.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == document.CurrentStateId);
                if (state == null)
                    return false;
            }

            if (state.IsFinal)
                return false;

            var employeePositionIds = await _dbContext.EmployeePositions
                .Where(ep => ep.EmployeeId == employeeId && ep.EndDate == null && ep.PositionId != null)
                .Select(ep => ep.PositionId!.Value)
                .ToListAsync();

            bool isStateOwner = state.RequiredPositionId == null ? !state.IsInitial : employeePositionIds.Contains(state.RequiredPositionId.Value);
            if (isStateOwner)
                return true;

            // Если сотрудник не владелец состояния, проверяем, есть ли у него явно разрешённый переход
            var transitions = await _dbContext.WorkflowTransitions
                .Where(t => t.WorkflowId == document.WorkflowId && t.FromStateId == document.CurrentStateId)
                .ToListAsync();

            bool hasAllowedTransition = transitions.Any(t =>
            {
                if (!string.IsNullOrEmpty(t.AllowedPositionIds))
                {
                    var allowedIds = t.AllowedPositionIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => int.Parse(p.Trim()));
                    return allowedIds.Any(posId => employeePositionIds.Contains(posId));
                }
                return false;
            });

            return hasAllowedTransition;
        }

        public async Task<List<WorkflowTransition>> GetAvailableTransitionsFromInitialStateAsync(int workflowId, int employeeId)
        {
            var initial = await _dbContext.WorkflowStates
                .FirstOrDefaultAsync(s => s.WorkflowId == workflowId && s.IsInitial);
            if (initial == null)
                return new List<WorkflowTransition>();

            var temp = new Document
            {
                WorkflowId = workflowId,
                CurrentStateId = initial.Id
            };
            return await GetAvailableTransitionsAsync(temp, employeeId);
        }

        private static Dictionary<string, object> CloneExtraAttributes(Dictionary<string, object> source)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(source);
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                   ?? new Dictionary<string, object>();
        }
    }
}
