namespace Equipment_Service.Models;

public class EquipmentType
{
    public int Id { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public decimal AveragePrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public bool IsActive { get; set; } = true;
}
