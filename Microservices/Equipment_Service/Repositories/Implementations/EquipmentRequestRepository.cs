using Equipment_Service.Infrastructure.Data;
using Equipment_Service.Models;
using Equipment_Service.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Equipment_Service.Repositories.Implementations;

public class EquipmentRequestRepository : GenericRepository<EquipmentRequest>, IEquipmentRequestRepository
{
    public EquipmentRequestRepository(AppDbContext context) : base(context) { }

    public async Task<EquipmentRequest?> GetByIdWithDetailsAsync(int id) =>
        await _context.EquipmentRequests
            .Include(r => r.Requester)
            .Include(r => r.EquipmentType)
            .Include(r => r.WorkFlowSteps)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IEnumerable<EquipmentRequest>> GetByUserAsync(int employeeId) =>
        await _context.EquipmentRequests
            .Include(r => r.EquipmentType)
            .Where(r => r.RequesterId == employeeId)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();

    public async Task<IEnumerable<EquipmentType>> GetEquipmentTypesAsync() =>
        await _context.EquipmentTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.TypeName)
            .ToListAsync();

    public async Task CreateAsync(EquipmentRequest request) =>
        await _context.EquipmentRequests.AddAsync(request);
}
