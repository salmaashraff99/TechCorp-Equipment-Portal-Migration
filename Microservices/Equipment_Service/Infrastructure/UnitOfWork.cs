using Equipment_Service.Infrastructure.Data;
using Equipment_Service.Repositories.Implementations;
using Equipment_Service.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Equipment_Service.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;
    private readonly Dictionary<Type, object> _repositories = new();

    public IEquipmentRequestRepository Equipment { get; }
    public IWorkFlowRepository WorkFlow { get; }
    public IEmployeeRepository Employees { get; }

    public UnitOfWork(AppDbContext context,
        IEquipmentRequestRepository equipment,
        IWorkFlowRepository workFlow,
        IEmployeeRepository employees)
    {
        _context = context;
        Equipment = equipment;
        WorkFlow = workFlow;
        Employees = employees;
    }

    public IGenericRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);
        if (!_repositories.ContainsKey(type))
            _repositories[type] = new GenericRepository<T>(_context);
        return (IGenericRepository<T>)_repositories[type];
    }

    public async Task BeginTransactionAsync() =>
        _transaction = await _context.Database.BeginTransactionAsync();

    public async Task CommitAsync()
    {
        await _context.SaveChangesAsync();
        if (_transaction is not null)
            await _transaction.CommitAsync();
    }

    public async Task RollbackAsync()
    {
        if (_transaction is not null)
            await _transaction.RollbackAsync();
    }

    public async Task<int> SaveAsync() => await _context.SaveChangesAsync();

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
