using DocumEntum.Data;

namespace DocumEntum.Services
{
    public interface IDocumentService
    {
        // Создание документа (только для сотрудников)
        Task<Document> CreateDocumentAsync(string title, int workflowId, int authorId, int? departmentId,
            Dictionary<string, object> extraAttributes, Stream fileStream, string originalFileName, long fileSize, string contentType);

        Task<Document?> GetDocumentAsync(int id);
        Task<(Stream? FileStream, string ContentType, string FileName, string FileExtension)?> GetDocumentFileAsync(int id);

        // Обновление атрибутов (только для Employee, если документ в черновике)
        Task UpdateExtraAttributesAsync(int id, Dictionary<string, object> extraAttributes);

        // Удаление документа (администратор может удалить любой)
        Task DeleteDocumentAsync(int id);

        // Административные методы
        // для админов
        Task<List<Document>> GetAllDocumentsAsync(bool includeDeleted = false);
        Task<List<Document>> GetDocumentsByDepartmentAsync(int departmentId, bool includeDeleted = false);
        Task UpdateDocumentFileAsync(int documentId, Stream newFileStream, string originalFileName, long fileSize, string contentType);
    }
}
