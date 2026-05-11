using DocumEntum.Components.Pages.Admin;
using DocumEntum.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Mime;

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
        public async Task<Document> CreateDocumentAsync(string title, int workflowId, int authorId, int? departmentId,
            Dictionary<string, object> extraAttributes, Stream fileStream, string originalFileName, long fileSize, string contentType)
        {
            // Только сотрудник (Employee) может создавать документы
            var currentEmployee = await _currentUserService.GetCurrentEmployeeAsync();
            if (currentEmployee == null || currentEmployee.Id != authorId)
                throw new UnauthorizedAccessException("Только сотрудник может создавать документы.");
            // workflow и начальное состояние
            var workflow = await _dbContext.Workflows
                .Include(w => w.DocumentType)
                .FirstOrDefaultAsync(w => w.Id == workflowId);
            if (workflow == null) throw new Exception("Workflow not found");

            var initialState = await _dbContext.WorkflowStates
                .FirstOrDefaultAsync(s => s.WorkflowId == workflowId && s.IsInitial);
            if (initialState == null) throw new Exception("No initial state");
            // Сохраняем в файл
            var storedRelativePath = await _fileStorage.SaveFileAsync(fileStream, originalFileName, "pending");

            // Заполняем метаданные
            var document = new Document
            {
                Title = title,
                AuthorId = authorId,
                WorkflowId = workflowId,
                CurrentStateId = initialState.Id,
                DepartmentId = departmentId,
                DocumentTypeId = workflow.DocumentTypeId,
                ExtraAttributes = extraAttributes,
                FileName = originalFileName,
                FileExtension = Path.GetExtension(originalFileName).TrimStart('.'),
                StoredFileName = storedRelativePath,
                FileSize = fileSize,
                ContentType = contentType,
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

            // Удаляем старый файл
            await _fileStorage.DeleteFileAsync(document.StoredFileName);

            // Сохраняем новый файл
            var newRelativePath = await _fileStorage.SaveFileAsync(newFileStream, originalFileName, "pending");

            // Обновляем метаданные
            document.StoredFileName = newRelativePath;
            document.FileName = originalFileName;
            document.FileExtension = Path.GetExtension(originalFileName).TrimStart('.');
            document.FileSize = fileSize;
            document.ContentType = contentType;
            document.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
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

            // Если документ в утверждённом (конечном) состоянии – проверить отделы из типа документа
            if (doc.CurrentState != null && doc.CurrentState.IsFinal && doc.DocumentType != null)
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

            // Только автор или администратор может менять атрибуты, но администратор – только в исключительных случаях
            var employee = await _currentUserService.GetCurrentEmployeeAsync();
            if (employee == null || (doc.AuthorId != employee.Id && !await _currentUserService.IsAdminAsync()))
                throw new UnauthorizedAccessException("Нет прав на изменение атрибутов документа.");

            doc.ExtraAttributes = extraAttributes;
            doc.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
        public async Task DeleteDocumentAsync(int id)
        {
            var doc = await _dbContext.Documents.FindAsync(id);
            if (doc == null) return;

            var isAdmin = await _currentUserService.IsAdminAsync();
            var employee = await _currentUserService.GetCurrentEmployeeAsync();
            var isAuthor = employee != null && doc.AuthorId == employee.Id;

            if (!isAdmin && !isAuthor)
                throw new UnauthorizedAccessException("Удалять документ может только автор или администратор.");

            // Мягкое удаление: просто ставим флаг
            doc.IsDeleted = true;
            doc.UpdatedAt = DateTime.UtcNow;
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
