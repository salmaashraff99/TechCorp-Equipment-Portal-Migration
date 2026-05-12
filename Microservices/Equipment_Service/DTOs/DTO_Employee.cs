namespace Equipment_Service.DTOs;

public class DTO_EmployeeLookup
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? ManagerId { get; set; }
}
