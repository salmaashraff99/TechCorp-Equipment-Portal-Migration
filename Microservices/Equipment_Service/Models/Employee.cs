namespace Equipment_Service.Models;

public class Employee
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string Role { get; set; } = string.Empty;
    public int? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
}
