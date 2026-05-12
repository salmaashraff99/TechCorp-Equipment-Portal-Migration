using Equipment_Service.Repositories.Interfaces;

namespace Equipment_Service.Tests.MockRepository;

public class MockWorkFlowRepository : IWorkFlowRepository
{
    // Control flags
    public bool ShouldFail        { get; set; } = false;
    public bool HasLineManager    { get; set; } = true;
    public bool IsFinanceRequired { get; set; } = false;

    // Settable return values
    public WorkFlowStep?              PendingStepToReturn { get; set; } = EquipmentMockData.PendingStep1;
    public IEnumerable<WorkFlowStep>  HistoryToReturn     { get; set; } = EquipmentMockData.WorkFlowHistory;

    // Inspection
    public WorkFlowStep?          LastUpdated  { get; private set; }
    public List<WorkFlowStep>     CreatedSteps { get; } = new();
    private int _nextId = 200;

    // IGenericRepository<WorkFlowStep>
    public Task<WorkFlowStep?> GetByIdAsync(int id) =>
        Task.FromResult<WorkFlowStep?>(null);

    public Task<IEnumerable<WorkFlowStep>> GetAllAsync() =>
        Task.FromResult(HistoryToReturn);

    public Task AddAsync(WorkFlowStep entity)
    {
        entity.Id = _nextId++;
        CreatedSteps.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(WorkFlowStep entity) => LastUpdated = entity;

    public void Delete(WorkFlowStep entity) { }

    // IWorkFlowRepository
    public Task<WorkFlowStep?> GetPendingStepAsync(int requestId)
    {
        if (ShouldFail) return Task.FromResult<WorkFlowStep?>(null);
        return Task.FromResult(PendingStepToReturn);
    }

    public Task<IEnumerable<WorkFlowStep>> GetHistoryAsync(int requestId) =>
        Task.FromResult(ShouldFail ? Enumerable.Empty<WorkFlowStep>() : HistoryToReturn);

    public Task CreateWorkFlowStepAsync(WorkFlowStep step)
    {
        step.Id = _nextId++;
        CreatedSteps.Add(step);
        return Task.CompletedTask;
    }
}
