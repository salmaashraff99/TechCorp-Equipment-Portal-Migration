using Equipment_Service.Infrastructure;
using Equipment_Service.Repositories.Interfaces;

namespace Equipment_Service.Tests.MockUoW;

public class MockUnitOfWork : IUnitOfWork
{
    public MockEquipmentRequestRepository MockEquipment  { get; }
    public MockWorkFlowRepository         MockWorkFlow   { get; }
    public MockEmployeeRepository         MockEmployees  { get; }

    // IUnitOfWork explicit interface members
    public IEquipmentRequestRepository Equipment  => MockEquipment;
    public IWorkFlowRepository         WorkFlow   => MockWorkFlow;
    public IEmployeeRepository         Employees  => MockEmployees;

    public MockUnitOfWork()
    {
        MockEquipment = new MockEquipmentRequestRepository();
        MockWorkFlow  = new MockWorkFlowRepository();
        MockEmployees = new MockEmployeeRepository();
    }

    public IGenericRepository<T> Repository<T>() where T : class =>
        throw new NotImplementedException("Not needed in unit tests.");

    public Task BeginTransactionAsync() => Task.CompletedTask;
    public Task CommitAsync()           => Task.CompletedTask;
    public Task RollbackAsync()         => Task.CompletedTask;
    public Task<int> SaveAsync()        => Task.FromResult(1);

    public void Dispose() { }
}
