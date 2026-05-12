using Equipment_Service.Infrastructure.Data;
using Equipment_Service.Models;
using Equipment_Service.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Equipment_Service.Repositories.Implementations;

public class EmployeeRepository : GenericRepository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(AppDbContext context) : base(context) { }

    public new async Task<Employee?> GetByIdAsync(int id) =>
        await _context.Employees.FirstOrDefaultAsync(e => e.Id == id && e.IsActive);

    public async Task<Employee?> GetLineManagerAsync(int employeeId)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee?.ManagerId is null) return null;
        return await _context.Employees.FirstOrDefaultAsync(e => e.Id == employee.ManagerId && e.IsActive);
    }

    public async Task<Employee?> GetDepartmentHeadAsync(int departmentId) =>
        await _context.Employees.FirstOrDefaultAsync(e =>
            e.DepartmentId == departmentId && e.Role == "DeptHead" && e.IsActive);

    public async Task<Employee?> GetByRoleAsync(string role) =>
        await _context.Employees.FirstOrDefaultAsync(e => e.Role == role && e.IsActive);
}
