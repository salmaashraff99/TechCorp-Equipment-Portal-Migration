namespace Equipment_Service.Models;

public class EquipmentRequestItem
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int EquipmentTypeId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;

    public EquipmentRequest Request { get; set; } = null!;
    public EquipmentType EquipmentType { get; set; } = null!;
}
