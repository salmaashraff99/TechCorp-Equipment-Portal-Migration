using Equipment_Service.DTOs;
using Equipment_Service.Infrastructure;
using Equipment_Service.Models;
using TechCorp.Shared.DTOs;
using TechCorp.Shared.Enums;

namespace Equipment_Service.Services;

public class EquipmentRequest_Service
{
    private const int StatusDraft           = 1;
    private const int StatusPendingManager  = 2;
    private const int StepPending           = 2;

    private readonly IUnitOfWork _uow;

    public EquipmentRequest_Service(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<DTO_Response<DTO_RequestResponse>> GetByIdAsync(int id)
    {
        var request = await _uow.Equipment.GetByIdWithDetailsAsync(id);
        if (request is null)
            return Fail<DTO_RequestResponse>((int)StatusCode.NotFound, "Request not found.");

        return Ok(new List<DTO_RequestResponse> { MapToResponse(request) });
    }

    public async Task<DTO_Response<DTO_RequestResponse>> GetByUserAsync(int employeeId)
    {
        var requests = await _uow.Equipment.GetByUserAsync(employeeId);
        var data = requests.Select(MapToResponse).ToList();
        return Ok(data);
    }

    public async Task<DTO_Response<string>> SubmitAsync(DTO_SubmitRequest dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Justification) || dto.Justification.Trim().Length < 10)
            return Fail<string>((int)StatusCode.NotAcceptable, "Justification is required (minimum 10 characters).");

        await _uow.BeginTransactionAsync();
        try
        {
            var requester = await _uow.Employees.GetByIdAsync(dto.RequesterId);
            if (requester is null)
                return Fail<string>((int)StatusCode.NotFound, "Requester not found.");

            var manager = await _uow.Employees.GetLineManagerAsync(dto.RequesterId);
            if (manager is null)
                return Fail<string>((int)StatusCode.NotAcceptable, "No line manager found for this employee. Cannot submit request.");

            if (manager.Id == dto.RequesterId)
                return Fail<string>((int)StatusCode.NotAcceptable, "Requester cannot be their own line manager.");

            var equipmentType = await _uow.Equipment.GetByIdAsync(dto.EquipmentTypeId);
            if (equipmentType is null)
                return Fail<string>((int)StatusCode.NotFound, "Equipment type not found.");

            var request = new EquipmentRequest
            {
                RequesterId     = dto.RequesterId,
                EquipmentTypeId = dto.EquipmentTypeId,
                Quantity        = dto.Quantity,
                Priority        = dto.Priority,
                EstimatedCost   = dto.EstimatedCost,
                Justification   = dto.Justification,
                Department      = dto.Department,
                Status          = StatusPendingManager,
                RequestDate     = DateTime.UtcNow,
                SubmitDate      = DateTime.UtcNow
            };

            await _uow.Equipment.CreateAsync(request);
            await _uow.SaveAsync();

            // create first workflow step (Line Manager)
            await _uow.WorkFlow.CreateWorkFlowStepAsync(new WorkFlowStep
            {
                RequestId   = request.Id,
                StepNumber  = 1,
                StepName    = "Line Manager Approval",
                ApproverId  = manager.Id,
                Status      = StepPending,
                CreatedDate = DateTime.UtcNow
            });

            await _uow.CommitAsync();

            return new DTO_Response<string>
            {
                code    = (int)StatusCode.Created,
                error   = false,
                message = $"Request submitted successfully. Reference #{request.Id}",
                data    = new List<string> { request.Id.ToString() }
            };
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return Fail<string>((int)StatusCode.InternalServerError, ex.Message);
        }
    }

    public async Task<DTO_Response<DTO_WorkFlowHistoryItem>> GetHistoryAsync(int requestId)
    {
        var steps = await _uow.WorkFlow.GetHistoryAsync(requestId);
        var data = steps.Select(s => new DTO_WorkFlowHistoryItem
        {
            StepNumber   = s.StepNumber,
            StepName     = s.StepName,
            ApproverName = s.ApproverId.HasValue ? $"Employee #{s.ApproverId}" : "Pending",
            StatusName   = s.Status == 2 ? "Pending" : s.Status == 3 ? "Approved" : "Rejected",
            Comments     = s.Comments,
            ActionDate   = s.ActionDate
        }).ToList();

        return Ok(data);
    }

    public async Task<DTO_Response<EquipmentType>> GetEquipmentTypesAsync()
    {
        var types = (await _uow.Equipment.GetEquipmentTypesAsync()).ToList();
        return Ok(types);
    }

    private static DTO_RequestResponse MapToResponse(EquipmentRequest r) => new()
    {
        Id               = r.Id,
        RequesterId      = r.RequesterId,
        RequesterName    = r.Requester?.FullName ?? string.Empty,
        Department       = r.Department,
        EquipmentTypeName = r.EquipmentType?.TypeName ?? string.Empty,
        Quantity         = r.Quantity,
        PriorityName     = r.Priority switch { 1 => "Low", 2 => "Normal", 3 => "High", 4 => "Critical", _ => "Unknown" },
        EstimatedCost    = r.EstimatedCost,
        Justification    = r.Justification,
        StatusName       = r.Status switch
        {
            1  => "Draft",
            2  => "Pending Line Manager",
            3  => "Pending IT Head",
            4  => "Pending Finance",
            5  => "Pending IT Operations",
            6  => "Fully Approved",
            7  => "Rejected by Line Manager",
            8  => "Rejected by IT Head",
            9  => "Rejected by Finance",
            10 => "Rejected by IT Operations",
            11 => "Cancelled",
            _  => "Unknown"
        },
        RequestDate      = r.RequestDate,
        SubmitDate       = r.SubmitDate
    };

    private static DTO_Response<T> Ok<T>(List<T> data) => new()
    {
        code = (int)StatusCode.OK, error = false, message = "Success", data = data
    };

    private static DTO_Response<T> Fail<T>(int code, string message) => new()
    {
        code = code, error = true, message = message, data = new List<T>()
    };
}
