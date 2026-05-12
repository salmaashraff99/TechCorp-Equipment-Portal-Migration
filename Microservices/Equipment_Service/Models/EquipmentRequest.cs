namespace Equipment_Service.Models;

public class EquipmentRequest
{
    public int Id { get; set; }
    public int RequesterId { get; set; }
    public int EquipmentTypeId { get; set; }
    public int Quantity { get; set; }
    public int Priority { get; set; }
    public decimal EstimatedCost { get; set; }
    public string Justification { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public DateTime? SubmitDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? RejectionReason { get; set; }

    public Employee Requester { get; set; } = null!;
    public EquipmentType EquipmentType { get; set; } = null!;
    public ICollection<WorkFlowStep> WorkFlowSteps { get; set; } = new List<WorkFlowStep>();
}
