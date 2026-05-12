namespace Equipment_Service.Tests.MockData;

public static class EquipmentMockData
{
    // ── Employees ────────────────────────────────────────────────────────────

    public static Employee Requester => new()
    {
        Id = 1, FullName = "John Doe", Email = "john.doe@techcorp.com",
        Username = "jdoe", Department = "Engineering", DepartmentId = 3,
        Role = "Employee", ManagerId = 2, IsActive = true
    };

    public static Employee LineManager => new()
    {
        Id = 2, FullName = "Jane Smith", Email = "jane.smith@techcorp.com",
        Username = "jsmith", Department = "Engineering", DepartmentId = 3,
        Role = "Manager", ManagerId = null, IsActive = true
    };

    // ── Equipment Types ───────────────────────────────────────────────────────

    public static EquipmentType LaptopType => new()
    {
        Id = 1, TypeName = "Laptop",
        AveragePrice = 2500, MinPrice = 1500, MaxPrice = 4000, IsActive = true
    };

    public static EquipmentType WorkstationType => new()
    {
        Id = 2, TypeName = "High-End Workstation",
        AveragePrice = 8000, MinPrice = 6000, MaxPrice = 12000, IsActive = true
    };

    // ── Submit DTOs ───────────────────────────────────────────────────────────

    public static DTO_SubmitRequest ValidSubmitRequest => new()
    {
        RequesterId     = 1,
        EquipmentTypeId = 1,
        Quantity        = 1,
        Priority        = 2,
        EstimatedCost   = 3000m,
        Justification   = "Required for software development work on the new portal project.",
        Department      = "Engineering"
    };

    public static DTO_SubmitRequest HighCostSubmitRequest => new()
    {
        RequesterId     = 1,
        EquipmentTypeId = 2,
        Quantity        = 1,
        Priority        = 3,
        EstimatedCost   = 8000m,
        Justification   = "High-performance workstation required for 3D rendering pipeline.",
        Department      = "Engineering"
    };

    // ── Existing Requests ─────────────────────────────────────────────────────

    public static EquipmentRequest PendingManagerRequest => new()
    {
        Id              = 10,
        RequesterId     = 1,
        EquipmentTypeId = 1,
        Quantity        = 1,
        Priority        = 2,
        EstimatedCost   = 3000m,
        Justification   = "Required for software development.",
        Department      = "Engineering",
        Status          = 2,
        RequestDate     = DateTime.UtcNow.AddDays(-1),
        Requester       = Requester,
        EquipmentType   = LaptopType,
        WorkFlowSteps   = new List<WorkFlowStep>()
    };

    public static EquipmentRequest HighCostPendingRequest => new()
    {
        Id              = 11,
        RequesterId     = 1,
        EquipmentTypeId = 2,
        Quantity        = 1,
        Priority        = 3,
        EstimatedCost   = 8000m,
        Justification   = "Workstation for 3D rendering.",
        Department      = "Engineering",
        Status          = 3,
        RequestDate     = DateTime.UtcNow.AddDays(-2),
        Requester       = Requester,
        EquipmentType   = WorkstationType,
        WorkFlowSteps   = new List<WorkFlowStep>()
    };

    public static EquipmentRequest ITOpsPendingRequest => new()
    {
        Id              = 12,
        RequesterId     = 1,
        EquipmentTypeId = 1,
        Quantity        = 1,
        Priority        = 2,
        EstimatedCost   = 3000m,
        Justification   = "Required for development.",
        Department      = "Engineering",
        Status          = 5,
        RequestDate     = DateTime.UtcNow.AddDays(-5),
        Requester       = Requester,
        EquipmentType   = LaptopType,
        WorkFlowSteps   = new List<WorkFlowStep>()
    };

    // ── WorkFlow Steps ────────────────────────────────────────────────────────

    public static WorkFlowStep PendingStep1 => new()
    {
        Id = 1, RequestId = 10, StepNumber = 1,
        StepName = "Line Manager Approval", Status = 2,
        CreatedDate = DateTime.UtcNow.AddDays(-1)
    };

    public static WorkFlowStep PendingStep2 => new()
    {
        Id = 2, RequestId = 11, StepNumber = 2,
        StepName = "IT Department Head Approval", Status = 2,
        CreatedDate = DateTime.UtcNow.AddDays(-1)
    };

    public static WorkFlowStep PendingStep4 => new()
    {
        Id = 4, RequestId = 12, StepNumber = 4,
        StepName = "IT Operations Fulfillment", Status = 2,
        CreatedDate = DateTime.UtcNow.AddDays(-1)
    };

    public static IEnumerable<WorkFlowStep> WorkFlowHistory => new List<WorkFlowStep>
    {
        new()
        {
            Id = 1, RequestId = 10, StepNumber = 1,
            StepName = "Line Manager Approval", ApproverId = 2,
            Status = 3, Comments = "Looks good.",
            ActionDate = DateTime.UtcNow.AddDays(-3), CreatedDate = DateTime.UtcNow.AddDays(-4)
        },
        new()
        {
            Id = 2, RequestId = 10, StepNumber = 2,
            StepName = "IT Department Head Approval", ApproverId = null,
            Status = 2, Comments = null,
            ActionDate = null, CreatedDate = DateTime.UtcNow.AddDays(-3)
        }
    };

    // ── Approval / Reject DTOs ────────────────────────────────────────────────

    public static DTO_ApproveRequest Level1ApprovalDto => new()
    {
        RequestId = 10, ApproverId = 2, Level = 1, Comments = "Approved by line manager."
    };

    public static DTO_ApproveRequest Level4ApprovalDto => new()
    {
        RequestId = 12, ApproverId = 2, Level = 4, Comments = "Equipment ordered and dispatched."
    };

    public static DTO_RejectRequest Level1RejectDto => new()
    {
        RequestId = 10, ApproverId = 2, Level = 1, Reason = "Budget constraints this quarter."
    };

    // All requests for GetByUser
    public static IEnumerable<EquipmentRequest> UserRequests => new List<EquipmentRequest>
    {
        PendingManagerRequest,
        HighCostPendingRequest
    };
}
