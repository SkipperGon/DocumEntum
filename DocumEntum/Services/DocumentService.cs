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

        public DocumentService(ApplicationDbContext dbContext, IFileStorageService fileStorage, ICurrentUserService currentUserService)
        {
            _dbContext = dbContext;
            _fileStorage = fileStorage;
            _currentUserService = currentUserService;
        }
        public async Task<Document> CreateDocumentAsync(string title, int workflowId, int authorId, int? departmentId,
            Dictionary<string, object> extraAttributes, Stream fileStream, string originalFileName, long fileSize, string contentType)
        {
            // Только сотрудник (Employee) может создавать документы
            var currentEmployee = await _currentUserService.GetCurrentEmployeeAsync();
            if (currentEmployee == null || currentEmployee.Id != authorId)
                throw new UnauthorizedAccessException("Только сотрудник может создавать документы.");
            // workflow и начальное состояние
            var workflow = await _dbContext.Workflows.Include(w => w.States).FirstOrDefaultAsync(w => w.Id == workflowId);
            if (workflow == null) throw new Exception("Workflow not found");
            var initialState = workflow.States.FirstOrDefault(s => s.IsInitial) ?? throw new Exception("No initial state");
            // Сохраняем в файл
            var storedGuid = await _fileStorage.SaveFileAsync(fileStream, originalFileName);
            // Заполняем метаданные
            var document = new Document
            {
                Title = title,
                AuthorId = authorId,
                WorkflowId = workflowId,
                CurrentStateId = initialState.Id,
                DepartmentId = departmentId,
                ExtraAttributes = extraAttributes,
                FileName = originalFileName,
                FileExtension = Path.GetExtension(originalFileName).TrimStart('.'),
                StoredFileName = storedGuid,
                FileSize = fileSize,
                ContentType = contentType,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();
            return document;
        }
        public async Task<Document?> GetDocumentAsync(int id)
        {
            var doc = await _dbContext.Documents.FindAsync(id);
            if (doc == null) return null;

            // Администратор может смотреть любой документ
            if (await _currentUserService.IsAdminAsync())
                return doc;

            // Сотрудник может смотреть только документы своего отдела (или автора)
            var employee = await _currentUserService.GetCurrentEmployeeAsync();
            if (employee == null) return null;
            if (doc.AuthorId == employee.Id || doc.DepartmentId == await _currentUserService.GetDepartmentIdAsync())
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

            await _fileStorage.DeleteFileAsync(doc.StoredFileName);
            _dbContext.Documents.Remove(doc);
            await _dbContext.SaveChangesAsync();
        }
        public async Task<List<Document>> GetAllDocumentsAsync()
        {
            if (!await _currentUserService.IsAdminAsync())
                throw new UnauthorizedAccessException("Доступ только для администраторов.");
            return await _dbContext.Documents.ToListAsync();
        }
        public async Task<List<Document>> GetDocumentsByDepartmentAsync(int departmentId)
        {
            if (!await _currentUserService.IsAdminAsync())
                throw new UnauthorizedAccessException("Доступ только для администраторов.");
            return await _dbContext.Documents.Where(d => d.DepartmentId == departmentId).ToListAsync();
        }
    }
}
