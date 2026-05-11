using DocumEntum.Data;

namespace DocumEntum.Services
{
    public interface IWorkflowAdminService
    {
        Task<List<Workflow>> GetWorkflowsAsync();
        Task<Workflow?> GetWorkflowByIdAsync(int id);
        Task<Workflow> CreateWorkflowAsync(Workflow workflow, int documentTypeId);
        Task<Workflow> UpdateWorkflowAsync(Workflow workflow);
        Task DeleteWorkflowAsync(int id);
        Task<List<Workflow>> GetWorkflowsByDocumentTypeAsync(int documentTypeId);

        Task<List<WorkflowState>> GetStatesForWorkflowAsync(int workflowId);
        Task<WorkflowState?> GetStateByIdAsync(int id);
        Task<WorkflowState> CreateStateAsync(WorkflowState state);
        Task<WorkflowState> UpdateStateAsync(WorkflowState state);
        Task DeleteStateAsync(int id);

        Task<List<WorkflowTransition>> GetTransitionsForWorkflowAsync(int workflowId);
        Task<WorkflowTransition?> GetTransitionByIdAsync(int id);
        Task<WorkflowTransition> CreateTransitionAsync(WorkflowTransition transition);
        Task<WorkflowTransition> UpdateTransitionAsync(WorkflowTransition transition);
        Task DeleteTransitionAsync(int id);
    }
}