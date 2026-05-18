using DocumEntum.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumEntum.Services
{
    public class WorkflowAdminService : IWorkflowAdminService
    {
        private readonly ApplicationDbContext _dbContext;

        public WorkflowAdminService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Workflow>> GetWorkflowsAsync()
        {
            return await _dbContext.Workflows.Include(w => w.States).ToListAsync();
        }

        public async Task<Workflow?> GetWorkflowByIdAsync(int id)
        {
            return await _dbContext.Workflows
                .Include(w => w.States)
                .FirstOrDefaultAsync(w => w.Id == id);
        }

        public async Task<Workflow> CreateWorkflowAsync(Workflow workflow)
        {
            _dbContext.Workflows.Add(workflow);
            await _dbContext.SaveChangesAsync();
            return workflow;
        }

        public async Task DeleteWorkflowAsync(int id)
        {
            var hasDocuments = await _dbContext.Documents.AnyAsync(d => d.WorkflowId == id);
            if (hasDocuments)
                throw new InvalidOperationException("Невозможно удалить процесс, так как существуют документы и черновики, связанные с ним.");

            var workflow = await _dbContext.Workflows.FindAsync(id);
            if (workflow != null)
            {
                _dbContext.Workflows.Remove(workflow);
                await _dbContext.SaveChangesAsync();
            }
        }
        public async Task<Workflow> CreateWorkflowAsync(Workflow workflow, int documentTypeId)
        {
            var docType = await _dbContext.DocumentTypes.FindAsync(documentTypeId);
            if (docType == null)
                throw new Exception("Document type not found");

            workflow.DocumentTypeId = documentTypeId;
            _dbContext.Workflows.Add(workflow);
            await _dbContext.SaveChangesAsync();
            return workflow;
        }

        public async Task<Workflow> UpdateWorkflowAsync(Workflow workflow)
        {
            var existing = await _dbContext.Workflows
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == workflow.Id);
            if (existing == null)
                throw new Exception("Workflow not found");

            if (existing.DocumentTypeId != workflow.DocumentTypeId)
            {
                var hasDocuments = await _dbContext.Documents.AnyAsync(d => d.WorkflowId == workflow.Id);
                if (hasDocuments)
                    throw new InvalidOperationException("Нельзя изменить тип документа для процесса, который уже используется в документах.");
            }

            _dbContext.Workflows.Update(workflow);
            await _dbContext.SaveChangesAsync();
            return workflow;
        }

        public async Task<List<Workflow>> GetWorkflowsByDocumentTypeAsync(int documentTypeId)
        {
            return await _dbContext.Workflows
                .Where(w => w.DocumentTypeId == documentTypeId)
                .Include(w => w.States)
                .ToListAsync();
        }
        public async Task<List<WorkflowState>> GetStatesForWorkflowAsync(int workflowId)
        {
            return await _dbContext.WorkflowStates
                .Where(s => s.WorkflowId == workflowId)
                .Include(s => s.RequiredPosition)
                .OrderBy(s => s.Order)
                .ToListAsync();
        }

        public async Task<WorkflowState?> GetStateByIdAsync(int id)
        {
            return await _dbContext.WorkflowStates
                .Include(s => s.RequiredPosition)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<WorkflowState> CreateStateAsync(WorkflowState state)
        {
            _dbContext.WorkflowStates.Add(state);
            await _dbContext.SaveChangesAsync();
            return state;
        }

        public async Task<WorkflowState> UpdateStateAsync(WorkflowState state)
        {
            _dbContext.WorkflowStates.Update(state);
            await _dbContext.SaveChangesAsync();
            return state;
        }

        public async Task DeleteStateAsync(int id)
        {
            var state = await _dbContext.WorkflowStates.FindAsync(id);
            if (state != null)
            {
                _dbContext.WorkflowStates.Remove(state);
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<List<WorkflowTransition>> GetTransitionsForWorkflowAsync(int workflowId)
        {
            return await _dbContext.WorkflowTransitions
                .Where(t => t.WorkflowId == workflowId)
                .Include(t => t.FromState)
                .Include(t => t.ToState)
                .ToListAsync();
        }

        public async Task<WorkflowTransition?> GetTransitionByIdAsync(int id)
        {
            return await _dbContext.WorkflowTransitions
                .Include(t => t.FromState)
                .Include(t => t.ToState)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<WorkflowTransition> CreateTransitionAsync(WorkflowTransition transition)
        {
            _dbContext.WorkflowTransitions.Add(transition);
            await _dbContext.SaveChangesAsync();
            return transition;
        }

        public async Task<WorkflowTransition> UpdateTransitionAsync(WorkflowTransition transition)
        {
            _dbContext.WorkflowTransitions.Update(transition);
            await _dbContext.SaveChangesAsync();
            return transition;
        }

        public async Task DeleteTransitionAsync(int id)
        {
            var transition = await _dbContext.WorkflowTransitions.FindAsync(id);
            if (transition != null)
            {
                _dbContext.WorkflowTransitions.Remove(transition);
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}