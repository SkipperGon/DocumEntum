using DocumEntum.Components.Pages.Admin;
using DocumEntum.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Mime;

namespace DocumEntum.Services
{
    public interface IDocumentService
    {
        Task<Document> CreateDocumentAsync(string title, int workflowId, int authorId, int? departmentId,
            Dictionary<string, object> extraAttributes, Stream fileStream, string originalFileName, long fileSize, string contentType);

        Task<Document?> GetDocumentAsync(int id);
        Task<(Stream? FileStream, string ContentType, string FileName, string FileExtension)?> GetDocumentFileAsync(int id);
        Task UpdateExtraAttributesAsync(int id, Dictionary<string, object> extraAttributes);
        Task DeleteDocumentAsync(int id);
    }

    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IFileStorageService _fileStorage;

        public DocumentService(ApplicationDbContext dbContext, IFileStorageService fileStorage)
        {
            _dbContext = dbContext;
            _fileStorage = fileStorage;
        }

        public async Task<Document> CreateDocumentAsync(string title, int workflowId, int authorId, int? departmentId,
        Dictionary<string, object> extraAttributes, Stream fileStream, string originalFileName, long fileSize, string contentType)
        {
            // 1. Получаем workflow и начальное состояние
            var workflow = await _dbContext.Workflows.Include(w => w.States).FirstOrDefaultAsync(w => w.Id == workflowId);
            if (workflow == null) throw new Exception("Workflow not found");
            var initialState = workflow.States.FirstOrDefault(s => s.IsInitial) ?? throw new Exception("No initial state");

            // 2. Сохраняем физический файл – получаем GUID (без расширения)
            var storedGuid = await _fileStorage.SaveFileAsync(fileStream, originalFileName);

            // 3. Заполняем метаданные
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

        public async Task<(Stream? FileStream, string ContentType, string FileName, string FileExtension)?> GetDocumentFileAsync(int id)
        {
            var doc = await _dbContext.Documents.FindAsync(id);
            if (doc == null) return null;
            var stream = await _fileStorage.GetFileStreamAsync(doc.StoredFileName);
            if (stream == null) return null;
            return (stream, doc.ContentType, doc.FileName, doc.FileExtension);
        }
        public async Task<Document?> GetDocumentAsync(int id) => await _dbContext.Documents.FindAsync(id);

        public async Task UpdateExtraAttributesAsync(int id, Dictionary<string, object> extraAttributes)
        {
            var doc = await _dbContext.Documents.FindAsync(id);
            if (doc != null)
            {
                doc.ExtraAttributes = extraAttributes;
                doc.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
        }
        public async Task DeleteDocumentAsync(int id)
        {
            var doc = await _dbContext.Documents.FindAsync(id);
            if (doc != null)
            {
                await _fileStorage.DeleteFileAsync(doc.StoredFileName);
                _dbContext.Documents.Remove(doc);
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}
