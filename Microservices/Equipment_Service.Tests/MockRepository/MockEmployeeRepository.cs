using Equipment_Service.Repositories.Interfaces;

namespace Equipment_Service.Tests.MockRepository;

public class MockEmployeeRepository : IEmployeeRepository
{
    // Control flags
    public bool ShouldFail        { get; set; } = false;
    public bool HasLineManager    { get; set; } = true;
    public bool IsFinanceRequired { get; set; } = false;

    // When true, GetLineManagerAsync returns an employee with the same Id as the requester
    public bool SelfApproval { get; set; } = false;

    // IGenericRepository<Employee>
    public Task<Employee?> GetByIdAsync(int id)
    {
        if (ShouldFail) return Task.FromResult<Employee?>(null);
        return Task.FromResult<Employee?>(id == 1 ? EquipmentMockData.Requester
                                       : id == 2 ? EquipmentMockData.LineManager
                                       : null);
    }

    public Task<IEnumerable<Employee>> GetAllAsync() =>
        Task.FromResult<IEnumerable<Employee>>(new[] { EquipmentMockData.Requester, EquipmentMockData.LineManager });

    public Task AddAsync(Employee entity)    => Task.CompletedTask;
    public void Update(Employee entity)      { }
    public void Delete(Employee entity)      { }

    // IEmployeeRepository
    public Task<Employee?> GetLineManagerAsync(int employeeId)
    {
        if (!HasLineManager) return Task.FromResult<Employee?>(null);
        if (SelfApproval)
        {
            // Return manager with same Id as the requester → triggers self-approval guard
            return Task.FromResult<Employee?>(new Employee
            {
                Id = employeeId, FullName = "Self Manager", Email = "self@techcorp.com",
                Role = "Manager", IsActive = true
            });
        }
        return Task.FromResult<Employee?>(EquipmentMockData.LineManager);
    }

    public Task<Employee?> GetDepartmentHeadAsync(int departmentId) =>
        Task.FromResult<Employee?>(EquipmentMockData.LineManager);

    public Task<Employee?> GetByRoleAsync(string role) =>
        Task.FromResult<Employee?>(EquipmentMockData.LineManager);
}
