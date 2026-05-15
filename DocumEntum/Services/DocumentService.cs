using DocumEntum.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumEntum.Services
{

    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IFileStorageService _fileStorage;
        private readonly ICurrentUserService _currentUserService;
        private readonly IWorkflowService _workflowService;

        public DocumentService(ApplicationDbContext dbContext, IFileStorageService fileStorage, ICurrentUserService currentUserService, IWorkflowService workflowService)
        {
            _dbContext = dbContext;
            _fileStorage = fileStorage;
            _currentUserService = currentUserService;
            _workflowService = workflowService;
        }
        public async Task<List<Document>> GetAccessibleDocumentsAsync()
        {
            if (await _currentUserService.IsAdminAsync())
            {
                return await _dbContext.Documents
                    .Include(d => d.CurrentState)
                    .Include(d => d.DocumentType)
                    .Include(d => d.Author)
                    .Where(d => !d.IsDeleted)
                    .OrderByDescending(d => d.CreatedAt)
                    .ToListAsync();
            }

            var employee = await _currentUserService.GetCurrentEmployeeAsync();
            if (employee == null) return new List<Document>();

            var userDeptId = await _currentUserService.GetDepartmentIdAsync();

            // Получаем список ID всех активных должностей текущего сотрудника
            var employeePositionIds = await _dbContext.EmployeePositions
                .Where(ep => ep.EmployeeId == employee.Id && ep.EndDate == null && ep.PositionId != null)
                .Select(ep => ep.PositionId!.Value)
                .ToListAsync();

            // Собираем StateId, из которых у сотрудника есть дополнительные переходы (как у бухгалтера для возврата)
            var allTransitions = await _dbContext.WorkflowTransitions.ToListAsync();
            var allowedStateIds = new HashSet<int>();
            foreach (var t in allTransitions)
            {
                if (!string.IsNullOrEmpty(t.AllowedPositionIds))
                {
                    var allowedIds = t.AllowedPositionIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => int.Parse(p.Trim()));
                    if (allowedIds.Any(posId => employeePositionIds.Contains(posId)))
                    {
                        allowedStateIds.Add(t.FromStateId);
                    }
                }
            }

            // Мои документы (все не удалённые)
            var myDocuments = _dbContext.Documents
                .Include(d => d.CurrentState)
                .Include(d => d.DocumentType)
                .Include(d => d.Author)
                .Where(d => d.AuthorId == employee.Id && !d.IsDeleted);

            // Очередь: чужие неутверждённые, где я владелец этапа ИЛИ у меня есть разрешенный переход
            var queueInWorkflow = _dbContext.Documents
                .Include(d => d.CurrentState)
                .Include(d => d.DocumentType)
                .Include(d => d.Author)
                .Where(d => !d.IsDeleted && d.AuthorId != employee.Id && !d.CurrentState.IsFinal)
                .Where(d =>
                    (d.CurrentState.RequiredPositionId != null && employeePositionIds.Contains(d.CurrentState.RequiredPositionId.Value))
                    || (d.CurrentState.RequiredPositionId == null && !d.CurrentState.IsInitial)
                    || allowedStateIds.Contains(d.CurrentStateId)); // <-- Добавлен доступ по переходам

            // Утверждённые документы, доступные отделу текущего сотрудника
            var finalDocuments = _dbContext.Documents
                .Include(d => d.CurrentState)
                .Include(d => d.DocumentType)
                .Include(d => d.Author)
                .Where(d => d.CurrentState.IsFinal && !d.IsDeleted && d.ReplacesDocumentId == null);

            if (userDeptId.HasValue)
                finalDocuments = finalDocuments.Where(d => d.DocumentType.AvailableDepartments.Any(dept => dept.Id == userDeptId.Value));
            else
                finalDocuments = finalDocuments.Where(d => false);

            var documents = await myDocuments.Union(queueInWorkflow).Union(finalDocuments)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return documents;
        }

        public async Task<List<Document>> GetWorkflowDocumentsAsync()
        {
            if (await _currentUserService.IsAdminAsync())
            {
                return await _dbContext.Documents
                    .Include(d => d.CurrentState)
                    .Include(d => d.DocumentType)
                    .Include(d => d.Author)
                    .Include(d => d.Workflow)
                    .Where(d => !d.CurrentState.IsFinal && !d.IsDeleted)
                    .OrderByDescending(d => d.CreatedAt)
                    .ToListAsync();
            }

            var employee = await _currentUserService.GetCurrentEmployeeAsync();
            if (employee == null) return new List<Document>();

            // Получаем ID должностей
            var employeePositionIds = await _dbContext.EmployeePositions
                .Where(ep => ep.EmployeeId == employee.Id && ep.EndDate == null && ep.PositionId != null)
                .Select(ep => ep.PositionId!.Value)
                .ToListAsync();

            // Собираем StateId по разрешенным переходам
            var allTransitions = await _dbContext.WorkflowTransitions.ToListAsync();
            var allowedStateIds = new HashSet<int>();
            foreach (var t in allTransitions)
            {
                if (!string.IsNullOrEmpty(t.AllowedPositionIds))
                {
                    var allowedIds = t.AllowedPositionIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => int.Parse(p.Trim()));
                    if (allowedIds.Any(posId => employeePositionIds.Contains(posId)))
                    {
                        allowedStateIds.Add(t.FromStateId);
                    }
                }
            }

            return await _dbContext.Documents
                .Include(d => d.CurrentState)
                .Include(d => d.DocumentType)
                .Include(d => d.Author)
                .Include(d => d.Workflow)
                .Where(d => !d.CurrentState.IsFinal && !d.IsDeleted)
                .Where(d =>
                    d.AuthorId == employee.Id
                    || (d.CurrentState.RequiredPositionId != null && employeePositionIds.Contains(d.CurrentState.RequiredPositionId.Value))
                    || (d.CurrentState.RequiredPositionId == null && !d.CurrentState.IsInitial)
                    || allowedStateIds.Contains(d.CurrentStateId)) // <-- Добавлен доступ по переходам
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }
        
        public async Task<(Stream? FileStream, string ContentType, string FileName, string FileExtension)?> GetDocumentVersionFileAsync(int versionId)
        {
            var version = await _dbContext.DocumentVersions.FindAsync(versionId);
            if (version == null) return null;

            // Проверяем наличие прав доступа к основному документу
            var doc = await GetDocumentAsync(version.DocumentId);
            if (doc == null) return null;

            var stream = await _fileStorage.GetFileStreamAsync(version.StoredFileName);
            if (stream == null) return null;
            return (stream, version.ContentType, version.FileName, version.FileExtension);
        }
        public async Task<Document> CreateDocumentAsync(string title, int workflowId, int authorId, int? departmentId,
            Dictionary<string, object> extraAttributes, Stream? fileStream, string? originalFileName, long fileSize, string? contentType)
        {
            var currentEmployee = await _currentUserService.GetCurrentEmployeeAsync();
            if (currentEmployee == null || currentEmployee.Id != authorId)
                throw new UnauthorizedAccessException("Только сотрудник может создавать документы.");

            var workflow = await _dbContext.Workflows
                .Include(w => w.DocumentType)
                .FirstOrDefaultAsync(w => w.Id == workflowId);
            if (workflow == null) throw new Exception("Workflow not found");

            if (!await _workflowService.CanEmployeeStartWorkflowAsync(authorId, workflowId))
                throw new UnauthorizedAccessException("Вы не можете начать этот процесс: проверьте должность на начальном этапе или активность процесса.");

            var initialState = await _dbContext.WorkflowStates
                .FirstOrDefaultAsync(s => s.WorkflowId == workflowId && s.IsInitial);
            if (initialState == null) throw new Exception("No initial state");

            // Сохраняем в файл, ТОЛЬКО ЕСЛИ он был передан
            string storedRelativePath = string.Empty;
            string fileExt = string.Empty;
            if (fileStream != null && !string.IsNullOrEmpty(originalFileName))
            {
                storedRelativePath = await _fileStorage.SaveFileAsync(fileStream, originalFileName, DocumentStorageFolders.Workflows);
                fileExt = Path.GetExtension(originalFileName).TrimStart('.');
            }

            var document = new Document
            {
                Title = title,
                AuthorId = authorId,
                WorkflowId = workflowId,
                CurrentStateId = initialState.Id,
                DepartmentId = departmentId,
                DocumentTypeId = workflow.DocumentTypeId,
                ExtraAttributes = extraAttributes,
                FileName = originalFileName ?? string.Empty,
                FileExtension = fileExt,
                StoredFileName = storedRelativePath,
                FileSize = fileSize,
                ContentType = contentType ?? string.Empty,
                ApprovedVersion = 0,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();
            return document;
        }

        public async Task UpdateDocumentFileAsync(int documentId, Stream newFileStream, string originalFileName, long fileSize, string contentType)
        {
            var document = await _dbContext.Documents.FindAsync(documentId);
            if (document == null)
                throw new ArgumentException("Документ не найден");

            var employee = await _currentUserService.GetCurrentEmployeeAsync();
            if (employee == null)
                throw new UnauthorizedAccessException("Только сотрудник может редактировать документ");

            var canEdit = await _workflowService.CanEditDocumentAsync(document, employee.Id);
            if (!canEdit)
                throw new UnauthorizedAccessException("На данном этапе редактирование документа запрещено");

            // Удаляем старый файл, если он вообще существовал (ведь документ мог быть создан без файла)
            if (!string.IsNullOrEmpty(document.StoredFileName))
            {
                await _fileStorage.DeleteFileAsync(document.StoredFileName);
            }

            // Сохраняем новый файл
            var newRelativePath = await _fileStorage.SaveFileAsync(newFileStream, originalFileName, DocumentStorageFolders.Workflows);

            // Обновляем метаданные
            document.StoredFileName = newRelativePath;
            document.FileName = originalFileName;
            document.FileExtension = Path.GetExtension(originalFileName).TrimStart('.');
            document.FileSize = fileSize;
            document.ContentType = contentType;
            document.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
        }
        public async Task<List<Document>> GetApprovedDocumentsAsync(bool includeDeleted = false)
        {
            if (await _currentUserService.IsAdminAsync())
            {
                var query = _dbContext.Documents
                    .Include(d => d.CurrentState)
                    .Include(d => d.DocumentType)
                    .Include(d => d.Author)
                    .Where(d => d.CurrentState.IsFinal && d.ReplacesDocumentId == null);

                // Если не запрошены удаленные, скрываем их
                if (!includeDeleted)
                {
                    query = query.Where(d => !d.IsDeleted);
                }

                return await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
            }

            var employee = await _currentUserService.GetCurrentEmployeeAsync();
            if (employee == null) return new List<Document>();

            var userDeptId = await _currentUserService.GetDepartmentIdAsync();

            var queryEmp = _dbContext.Documents
                .Include(d => d.CurrentState)
                .Include(d => d.DocumentType)
                .Include(d => d.Author)
                .Where(d => d.CurrentState.IsFinal && !d.IsDeleted && d.ReplacesDocumentId == null); // Сотрудники никогда не видят удаленные

            if (userDeptId.HasValue)
                queryEmp = queryEmp.Where(d => d.AuthorId == employee.Id ||
                    d.DocumentType.AvailableDepartments.Any(dept => dept.Id == userDeptId.Value));
            else
                queryEmp = queryEmp.Where(d => d.AuthorId == employee.Id);

            return await queryEmp.OrderByDescending(d => d.CreatedAt).ToListAsync();
        }
        public async Task RestoreDocumentAsync(int id)
        {
            if (!await _currentUserService.IsAdminAsync())
                throw new UnauthorizedAccessException("Только администратор может восстанавливать документы.");

            var doc = await _dbContext.Documents.FindAsync(id);
            if (doc != null && doc.IsDeleted)
            {
                doc.IsDeleted = false;
                doc.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
        }
        public async Task<List<DocumentVersion>> GetApprovedDocumentVersionsAsync(int approvedDocumentId)
        {
            var doc = await GetDocumentAsync(approvedDocumentId);
            if (doc == null)
                throw new UnauthorizedAccessException("Нет доступа к документу или документ не найден.");

            return await _dbContext.DocumentVersions
                .Where(v => v.DocumentId == approvedDocumentId)
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync();
        }

        public async Task<Document> StartEditApprovedDocumentAsync(int approvedDocumentId, int workflowId, int authorId, int? departmentId)
        {
            var currentEmployee = await _currentUserService.GetCurrentEmployeeAsync();
            if (currentEmployee == null || currentEmployee.Id != authorId)
                throw new UnauthorizedAccessException("Только сотрудник может начинать процесс изменения.");

            var approved = await _dbContext.Documents
                .Include(d => d.CurrentState)
                .Include(d => d.DocumentType)
                .FirstOrDefaultAsync(d => d.Id == approvedDocumentId);
            if (approved == null || approved.CurrentState == null || !approved.CurrentState.IsFinal || approved.IsDeleted)
                throw new InvalidOperationException("Утверждённый документ не найден или недоступен.");

            if (approved.ReplacesDocumentId != null)
                throw new InvalidOperationException("Нельзя запустить изменение для черновика процесса.");

            var workflow = await _dbContext.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId);
            if (workflow == null)
                throw new InvalidOperationException("Бизнес-процесс не найден.");
            if (workflow.DocumentTypeId != approved.DocumentTypeId)
                throw new InvalidOperationException("Тип бизнес-процесса должен совпадать с типом утверждённого документа.");

            var hasActiveDraft = await (
                from d in _dbContext.Documents
                join s in _dbContext.WorkflowStates on d.CurrentStateId equals s.Id
                where d.ReplacesDocumentId == approvedDocumentId && !s.IsFinal
                select d).AnyAsync();
            if (hasActiveDraft)
                throw new InvalidOperationException("Для этого документа уже есть активный процесс изменения.");

            var initialState = await _dbContext.WorkflowStates
                .FirstOrDefaultAsync(s => s.WorkflowId == workflowId && s.IsInitial);
            if (initialState == null)
                throw new InvalidOperationException("У процесса нет начального состояния.");

            var storedPath = await _fileStorage.CopyFileAsync(approved.StoredFileName, DocumentStorageFolders.Workflows);

            var document = new Document
            {
                Title = approved.Title,
                AuthorId = authorId,
                WorkflowId = workflowId,
                CurrentStateId = initialState.Id,
                DepartmentId = departmentId ?? approved.DepartmentId,
                DocumentTypeId = approved.DocumentTypeId,
                ExtraAttributes = CloneExtraAttributes(approved.ExtraAttributes),
                FileName = approved.FileName,
                FileExtension = approved.FileExtension,
                StoredFileName = storedPath,
                FileSize = approved.FileSize,
                ContentType = approved.ContentType,
                ReplacesDocumentId = approvedDocumentId,
                ApprovedVersion = 0,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();
            return document;
        }

        private static Dictionary<string, object> CloneExtraAttributes(Dictionary<string, object> source)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(source);
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                   ?? new Dictionary<string, object>();
        }

        public async Task<List<DocumentHistory>> GetDocumentHistoryAsync(int documentId)
        {
            return await _dbContext.DocumentHistories
                .Include(h => h.ActionBy)
                .Where(h => h.DocumentId == documentId)
                .OrderBy(h => h.ActionAt)
                .ToListAsync();
        }
        
        public async Task<Document?> GetDocumentAsync(int id)
        {
            var doc = await _dbContext.Documents
                .Include(d => d.CurrentState)
                .Include(d => d.DocumentType)
                    .ThenInclude(dt => dt.AvailableDepartments)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc == null) return null;

            // Удалённые документы – только админам или автору
            if (doc.IsDeleted)
            {
                var isAdmin = await _currentUserService.IsAdminAsync();
                var employee = await _currentUserService.GetCurrentEmployeeAsync();
                var isAuthor = employee != null && doc.AuthorId == employee.Id;
                if (!isAdmin && !isAuthor)
                    return null;
            }

            // Администратор может смотреть любой документ
            if (await _currentUserService.IsAdminAsync())
                return doc;

            var emp = await _currentUserService.GetCurrentEmployeeAsync();
            if (emp == null) return null;

            // Автор всегда может смотреть
            if (doc.AuthorId == emp.Id)
                return doc;

            // Участник неутверждённого процесса на текущем этапе (бухгалтер, начальник и т.д.)
            if (doc.CurrentState != null && !doc.CurrentState.IsFinal)
            {
                if (await _workflowService.CanEmployeeAccessWorkflowDocumentAsync(doc, emp.Id))
                    return doc;
            }

            // Утверждённый документ в каталоге: только записи без черновика замены
            if (doc.CurrentState != null && doc.CurrentState.IsFinal && doc.DocumentType != null && doc.ReplacesDocumentId == null)
            {
                var userDeptId = await _currentUserService.GetDepartmentIdAsync();
                if (userDeptId.HasValue && doc.DocumentType.AvailableDepartments.Any(d => d.Id == userDeptId.Value))
                    return doc;
            }

            // Существующая логика: доступ по отделу документа
            if (doc.DepartmentId == await _currentUserService.GetDepartmentIdAsync())
                return doc;

            return null;
        }
        public async Task<(Stream? FileStream, string ContentType, string FileName, string FileExtension)?> GetDocumentFileAsync(int id)
        {
            var doc = await GetDocumentAsync(id);
            if (doc == null) return null;

            var stream = await _fileStorage.GetFileStreamAsync(doc.StoredFileName);
            if (stream == null) return null;
            return (stream, doc.ContentType, doc.FileName, doc.FileExtension);
        }
        public async Task UpdateExtraAttributesAsync(int id, Dictionary<string, object> extraAttributes)
        {
            var doc = await _dbContext.Documents.FindAsync(id);
            if (doc == null) return;

            var employee = await _currentUserService.GetCurrentEmployeeAsync();
            if (await _currentUserService.IsAdminAsync())
            {
                // администратор — исключительный доступ без проверки этапа
            }
            else
            {
                if (employee == null)
                    throw new UnauthorizedAccessException("Нет прав на изменение атрибутов документа.");
                if (!await _workflowService.CanEditDocumentAsync(doc, employee.Id))
                    throw new UnauthorizedAccessException("На этом этапе редактирование атрибутов запрещено (CanEdit или должность).");
            }

            doc.ExtraAttributes = extraAttributes;
            doc.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
        public async Task DeleteDocumentAsync(int id)
        {
            var doc = await _dbContext.Documents
                .Include(d => d.CurrentState)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return;

            var isAdmin = await _currentUserService.IsAdminAsync();
            var employee = await _currentUserService.GetCurrentEmployeeAsync();
            var isAuthor = employee != null && doc.AuthorId == employee.Id;

            if (!isAdmin && !isAuthor)
                throw new UnauthorizedAccessException("Удалять документ может только автор или администратор.");

            var isApprovedCatalog = doc.CurrentState != null && doc.CurrentState.IsFinal && doc.ReplacesDocumentId == null;

            if (isApprovedCatalog)
            {
                doc.IsDeleted = true;
                doc.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                return;
            }

            if (!string.IsNullOrEmpty(doc.StoredFileName))
                await _fileStorage.DeleteFileAsync(doc.StoredFileName);

            var histories = await _dbContext.DocumentHistories.Where(h => h.DocumentId == doc.Id).ToListAsync();
            _dbContext.DocumentHistories.RemoveRange(histories);
            _dbContext.Documents.Remove(doc);
            await _dbContext.SaveChangesAsync();
        }
        public async Task<List<Document>> GetAllDocumentsAsync(bool includeDeleted = false)
        {
            if (!await _currentUserService.IsAdminAsync())
                throw new UnauthorizedAccessException("Доступ только для администраторов.");

            return includeDeleted
                ? await _dbContext.Documents.ToListAsync()
                : await _dbContext.Documents.Where(d => !d.IsDeleted).ToListAsync();
        }
        public async Task<List<Document>> GetDocumentsByDepartmentAsync(int departmentId, bool includeDeleted = false)
        {
            if (!await _currentUserService.IsAdminAsync())
                throw new UnauthorizedAccessException("Доступ только для администраторов.");

            var query = _dbContext.Documents.Where(d => d.DepartmentId == departmentId);
            if (!includeDeleted)
                query = query.Where(d => !d.IsDeleted);
            return await query.ToListAsync();
        }
        public async Task<string?> GetLastCommentForDocumentAsync(int documentId)
        {
            var lastHistory = await _dbContext.DocumentHistories
                .Where(h => h.DocumentId == documentId)
                .OrderByDescending(h => h.ActionAt)
                .FirstOrDefaultAsync();
            return lastHistory?.Comment;
        }
    }
}
