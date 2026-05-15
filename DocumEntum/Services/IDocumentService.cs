using DocumEntum.Data;

namespace DocumEntum.Services
{
    public interface IDocumentService
    {
        // Создание документа (для сотрудников)
        Task<Document> CreateDocumentAsync(string title, int workflowId, int authorId, int? departmentId,
                Dictionary<string, object> extraAttributes, Stream? fileStream, string? originalFileName, long fileSize, string? contentType);

        Task<Document?> GetDocumentAsync(int id);
        Task<(Stream? FileStream, string ContentType, string FileName, string FileExtension)?> GetDocumentFileAsync(int id);

        Task UpdateExtraAttributesAsync(int id, Dictionary<string, object> extraAttributes);
        Task DeleteDocumentAsync(int id);
        Task<List<Document>> GetAccessibleDocumentsAsync();
        Task<List<Document>> GetApprovedDocumentsAsync(bool includeDeleted = false);
        Task RestoreDocumentAsync(int id);
        Task<List<Document>> GetWorkflowDocumentsAsync();
        Task<List<DocumentHistory>> GetDocumentHistoryAsync(int documentId);
        Task<List<DocumentVersion>> GetApprovedDocumentVersionsAsync(int approvedDocumentId);
        Task<Document> StartEditApprovedDocumentAsync(int approvedDocumentId, int workflowId, int authorId, int? departmentId);

        Task<List<Document>> GetAllDocumentsAsync(bool includeDeleted = false);
        Task<List<Document>> GetDocumentsByDepartmentAsync(int departmentId, bool includeDeleted = false);
        Task UpdateDocumentFileAsync(int documentId, Stream newFileStream, string originalFileName, long fileSize, string contentType);
        Task<string?> GetLastCommentForDocumentAsync(int documentId);
        Task<(Stream? FileStream, string ContentType, string FileName, string FileExtension)?> GetDocumentVersionFileAsync(int versionId);
    }
}