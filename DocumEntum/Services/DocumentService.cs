using DocumEntum.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumEntum.Services
{
    public interface IDocumentService
    {
        Task<Document> CreateDocumentAsync(string title, int workflowId, int authorId, int? departmentId, Dictionary<string, object> extraAttributes);
        Task<Document?> GetDocumentAsync(int id);
        Task UpdateExtraAttributesAsync(int id, Dictionary<string, object> extraAttributes);
    }

    public class DocumentService : IDocumentService
    {
        private readonly ApplicationDbContext _dbContext;

        public DocumentService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Document> CreateDocumentAsync(string title, int workflowId, int authorId, int? departmentId, Dictionary<string, object> extraAttributes)
        {
            var workflow = await _dbContext.Workflows.Include(w => w.States).FirstOrDefaultAsync(w => w.Id == workflowId);
            if (workflow == null) throw new Exception("Workflow not found");

            var initialState = workflow.States.FirstOrDefault(s => s.IsInitial);
            if (initialState == null) throw new Exception("Workflow has no initial state");

            var document = new Document
            {
                Title = title,
                AuthorId = authorId,
                WorkflowId = workflowId,
                CurrentStateId = initialState.Id,
                DepartmentId = departmentId,
                ExtraAttributes = extraAttributes,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Documents.Add(document);
            await _dbContext.SaveChangesAsync();
            return document;
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
    }
}
