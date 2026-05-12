namespace Equipment_Service.Models;

public class WorkFlowStep
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int StepNumber { get; set; }
    public string StepName { get; set; } = string.Empty;
    public int? ApproverId { get; set; }
    public int Status { get; set; }
    public string? Comments { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ActionDate { get; set; }

    public EquipmentRequest Request { get; set; } = null!;
}
