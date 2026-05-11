using DocumEntum.Data;

namespace DocumEntum.Services
{
    public interface IDocumentTypeService
    {
        Task<List<DocumentType>> GetAllAsync();
        Task<DocumentType?> GetByIdAsync(int id);
        Task<DocumentType> CreateAsync(DocumentType documentType, List<int> departmentIds);
        Task UpdateAsync(DocumentType documentType, List<int> departmentIds);
        Task DeleteAsync(int id);
        Task<List<Department>> GetAllDepartmentsAsync();
    }
}