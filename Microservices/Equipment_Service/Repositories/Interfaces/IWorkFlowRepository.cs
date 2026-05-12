using Equipment_Service.Models;

namespace Equipment_Service.Repositories.Interfaces;

public interface IWorkFlowRepository : IGenericRepository<WorkFlowStep>
{
    Task<WorkFlowStep?> GetPendingStepAsync(int requestId);
    Task<IEnumerable<WorkFlowStep>> GetHistoryAsync(int requestId);
    Task CreateWorkFlowStepAsync(WorkFlowStep step);
}
