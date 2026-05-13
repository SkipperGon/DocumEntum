using DocumEntum.Data;

namespace DocumEntum.Services
{
    public interface IWorkflowService
    {
        Task<List<WorkflowTransition>> GetAvailableTransitionsAsync(Document document, int employeeId);
        Task<bool> ExecuteTransitionAsync(Document document, string actionName, int employeeId, string? comment = null);
        // Проверяем может ли сотрудник заменять файлы
        Task<bool> CanEditDocumentAsync(Document document, int employeeId);

        /// <summary>Процессы, которые текущий сотрудник может начать (начальное состояние без должности или с его должностью).</summary>
        Task<List<Workflow>> GetStartableWorkflowsAsync();

        /// <summary>Может ли сотрудник начать указанный процесс (активный, начальное состояние и должность).</summary>
        Task<bool> CanEmployeeStartWorkflowAsync(int employeeId, int workflowId);

        /// <summary>
        /// Доступ к документу в неутверждённом процессе: автор; либо этап с требуемой должностью сотрудника;
        /// либо этап без должности, но не начальный (редкий «открытый» этап для всех сотрудников).
        /// На начальном этапе с RequiredPositionId == null видит только автор.
        /// </summary>
        Task<bool> CanEmployeeAccessWorkflowDocumentAsync(Document document, int employeeId);

        /// <summary>Переходы с начального этапа процесса, доступные сотруднику (до создания документа).</summary>
        Task<List<WorkflowTransition>> GetAvailableTransitionsFromInitialStateAsync(int workflowId, int employeeId);
    }
}
