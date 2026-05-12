using Equipment_Service.Repositories.Interfaces;

namespace Equipment_Service.Infrastructure;

public interface IUnitOfWork : IDisposable
{
    IEquipmentRequestRepository Equipment { get; }
    IWorkFlowRepository WorkFlow { get; }
    IEmployeeRepository Employees { get; }
    IGenericRepository<T> Repository<T>() where T : class;
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
    Task<int> SaveAsync();
}
