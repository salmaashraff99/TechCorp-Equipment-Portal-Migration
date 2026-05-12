namespace Equipment_Service.DTOs;

public class DTO_SubmitRequest
{
    public int RequesterId { get; set; }
    public int EquipmentTypeId { get; set; }
    public int Quantity { get; set; }
    public int Priority { get; set; }
    public decimal EstimatedCost { get; set; }
    public string Justification { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

public class DTO_ApproveRequest
{
    public int RequestId { get; set; }
    public int ApproverId { get; set; }
    public int Level { get; set; }
    public string Comments { get; set; } = string.Empty;
}

public class DTO_RejectRequest
{
    public int RequestId { get; set; }
    public int ApproverId { get; set; }
    public int Level { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class DTO_RequestResponse
{
    public int Id { get; set; }
    public int RequesterId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string EquipmentTypeName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string PriorityName { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public string Justification { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public DateTime? SubmitDate { get; set; }
}

public class DTO_WorkFlowHistoryItem
{
    public int StepNumber { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string ApproverName { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime? ActionDate { get; set; }
}
