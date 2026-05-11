using DocumEntum.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumEntum.Services
{
    public class DocumentTypeService : IDocumentTypeService
    {
        private readonly ApplicationDbContext _dbContext;

        public DocumentTypeService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<DocumentType>> GetAllAsync()
        {
            return await _dbContext.DocumentTypes
                .Include(dt => dt.AvailableDepartments)
                .Include(dt => dt.Workflows)
                .ToListAsync();
        }

        public async Task<DocumentType?> GetByIdAsync(int id)
        {
            return await _dbContext.DocumentTypes
                .Include(dt => dt.AvailableDepartments)
                .Include(dt => dt.Workflows)
                .FirstOrDefaultAsync(dt => dt.Id == id);
        }

        public async Task<DocumentType> CreateAsync(DocumentType documentType, List<int> departmentIds)
        {
            if (departmentIds.Any())
            {
                var departments = await _dbContext.Departments
                    .Where(d => departmentIds.Contains(d.Id))
                    .ToListAsync();
                documentType.AvailableDepartments = departments;
            }

            _dbContext.DocumentTypes.Add(documentType);
            await _dbContext.SaveChangesAsync();
            return documentType;
        }

        public async Task UpdateAsync(DocumentType documentType, List<int> departmentIds)
        {
            var existing = await _dbContext.DocumentTypes
                .Include(dt => dt.AvailableDepartments)
                .FirstOrDefaultAsync(dt => dt.Id == documentType.Id);
            if (existing == null)
                throw new Exception("Document type not found");

            existing.Name = documentType.Name;
            existing.Description = documentType.Description;

            // Обновляем доступные отделы
            existing.AvailableDepartments.Clear();
            if (departmentIds.Any())
            {
                var departments = await _dbContext.Departments
                    .Where(d => departmentIds.Contains(d.Id))
                    .ToListAsync();
                foreach (var dept in departments)
                    existing.AvailableDepartments.Add(dept);
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var docType = await _dbContext.DocumentTypes.FindAsync(id);
            if (docType != null)
            {
                // Проверяем, есть ли привязанные workflow
                var hasWorkflows = await _dbContext.Workflows.AnyAsync(w => w.DocumentTypeId == id);
                if (hasWorkflows)
                    throw new InvalidOperationException("Невозможно удалить тип документа, т.к. к нему привязаны бизнес-процессы");

                _dbContext.DocumentTypes.Remove(docType);
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<List<Department>> GetAllDepartmentsAsync()
        {
            return await _dbContext.Departments.ToListAsync();
        }
    }
}