using Equipment_Service.Repositories.Interfaces;

namespace Equipment_Service.Tests.MockRepository;

public class MockEquipmentRequestRepository : IEquipmentRequestRepository
{
    // Control flags
    public bool ShouldFail        { get; set; } = false;
    public bool HasLineManager    { get; set; } = true;
    public bool IsFinanceRequired { get; set; } = false;

    // Settable return values for specific tests
    public EquipmentRequest? RequestToReturn { get; set; } = EquipmentMockData.PendingManagerRequest;

    // Inspection: what was last updated / created
    public EquipmentRequest?          LastUpdated    { get; private set; }
    public List<EquipmentRequest>     CreatedRequests { get; } = new();
    private int _nextId = 100;

    // IGenericRepository<EquipmentRequest>
    public Task<EquipmentRequest?> GetByIdAsync(int id)
    {
        // Used by SubmitAsync to verify equipment type exists; return non-null for any valid id
        if (ShouldFail) return Task.FromResult<EquipmentRequest?>(null);
        return Task.FromResult<EquipmentRequest?>(id > 0 ? EquipmentMockData.PendingManagerRequest : null);
    }

    public Task<IEnumerable<EquipmentRequest>> GetAllAsync() =>
        Task.FromResult<IEnumerable<EquipmentRequest>>(EquipmentMockData.UserRequests);

    public Task AddAsync(EquipmentRequest entity)
    {
        entity.Id = _nextId++;
        CreatedRequests.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(EquipmentRequest entity) => LastUpdated = entity;

    public void Delete(EquipmentRequest entity) { }

    // IEquipmentRequestRepository
    public Task<EquipmentRequest?> GetByIdWithDetailsAsync(int id)
    {
        if (ShouldFail) return Task.FromResult<EquipmentRequest?>(null);
        return Task.FromResult(RequestToReturn);
    }

    public Task<IEnumerable<EquipmentRequest>> GetByUserAsync(int employeeId) =>
        Task.FromResult(ShouldFail
            ? Enumerable.Empty<EquipmentRequest>()
            : EquipmentMockData.UserRequests.Where(r => r.RequesterId == employeeId));

    public Task<IEnumerable<EquipmentType>> GetEquipmentTypesAsync() =>
        Task.FromResult<IEnumerable<EquipmentType>>(new List<EquipmentType>
        {
            EquipmentMockData.LaptopType,
            EquipmentMockData.WorkstationType
        });

    public Task CreateAsync(EquipmentRequest request)
    {
        request.Id = _nextId++;
        CreatedRequests.Add(request);
        return Task.CompletedTask;
    }
}
