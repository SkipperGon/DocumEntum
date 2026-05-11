using DocumEntum.Data;

namespace DocumEntum.Services
{
    public interface IWorkflowService
    {
        Task<List<WorkflowTransition>> GetAvailableTransitionsAsync(Document document, int employeeId);
        Task<bool> ExecuteTransitionAsync(Document document, string actionName, int employeeId, string? comment = null);
        // Проверяем может ли сотрудник заменять файлы
        Task<bool> CanEditDocumentAsync(Document document, int employeeId);
    }
}
