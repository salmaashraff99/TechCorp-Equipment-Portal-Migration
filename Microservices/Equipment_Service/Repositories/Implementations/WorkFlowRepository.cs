using Equipment_Service.Infrastructure.Data;
using Equipment_Service.Models;
using Equipment_Service.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Equipment_Service.Repositories.Implementations;

public class WorkFlowRepository : GenericRepository<WorkFlowStep>, IWorkFlowRepository
{
    public WorkFlowRepository(AppDbContext context) : base(context) { }

    public async Task<WorkFlowStep?> GetPendingStepAsync(int requestId) =>
        await _context.WorkFlowSteps
            .Where(s => s.RequestId == requestId && s.Status == 2)
            .OrderBy(s => s.StepNumber)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<WorkFlowStep>> GetHistoryAsync(int requestId) =>
        await _context.WorkFlowSteps
            .Where(s => s.RequestId == requestId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync();

    public async Task CreateWorkFlowStepAsync(WorkFlowStep step) =>
        await _context.WorkFlowSteps.AddAsync(step);
}
