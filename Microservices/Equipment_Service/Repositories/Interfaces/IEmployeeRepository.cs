using Equipment_Service.Models;

namespace Equipment_Service.Repositories.Interfaces;

public interface IEmployeeRepository : IGenericRepository<Employee>
{
    new Task<Employee?> GetByIdAsync(int id);
    Task<Employee?> GetLineManagerAsync(int employeeId);
    Task<Employee?> GetDepartmentHeadAsync(int departmentId);
    Task<Employee?> GetByRoleAsync(string role);
}
