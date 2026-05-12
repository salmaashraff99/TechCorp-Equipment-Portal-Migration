using Equipment_Service.DTOs;
using Equipment_Service.Infrastructure;
using Equipment_Service.Models;
using TechCorp.Shared.DTOs;
using TechCorp.Shared.Enums;

namespace Equipment_Service.Services;

public class WorkFlow_Service
{
    private const decimal FinanceThreshold = 5000m;
    private const int DeptIdIT      = 7;
    private const int DeptIdFinance = 4;

    // Workflow statuses
    private const int StepPending  = 2;
    private const int StepApproved = 3;
    private const int StepRejected = 4;

    // Request statuses
    private const int StatusPendingManager  = 2;
    private const int StatusPendingITHead   = 3;
    private const int StatusPendingFinance  = 4;
    private const int StatusPendingITOps    = 5;
    private const int StatusFullyApproved   = 6;
    private const int StatusRejectedBase    = 7; // 7=Mgr, 8=ITHead, 9=Finance, 10=ITOps

    private readonly IUnitOfWork _uow;
    private readonly IHttpClientFactory _httpClientFactory;

    public WorkFlow_Service(IUnitOfWork uow, IHttpClientFactory httpClientFactory)
    {
        _uow = uow;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<DTO_Response<string>> ApproveAsync(DTO_ApproveRequest dto)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            var request = await _uow.Equipment.GetByIdWithDetailsAsync(dto.RequestId);
            if (request is null)
                return Fail<string>((int)StatusCode.NotFound, "Request not found.");

            if (request.RequesterId == dto.ApproverId)
                return Fail<string>((int)StatusCode.NotAcceptable, "You cannot approve your own request.");

            var pendingStep = await _uow.WorkFlow.GetPendingStepAsync(dto.RequestId);
            if (pendingStep is null || pendingStep.StepNumber != dto.Level)
                return Fail<string>((int)StatusCode.NotAcceptable, $"No pending step at level {dto.Level} for this request.");

            // mark current step approved
            pendingStep.Status     = StepApproved;
            pendingStep.ApproverId = dto.ApproverId;
            pendingStep.Comments   = dto.Comments;
            pendingStep.ActionDate = DateTime.UtcNow;
            _uow.WorkFlow.Update(pendingStep);

            await AdvanceWorkFlowAsync(request, dto.Level, dto.ApproverId);

            await _uow.CommitAsync();

            await SendNotificationAsync(request.Id, dto.ApproverId, "Approved", dto.Level);

            return Ok<string>("Request approved successfully.");
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return Fail<string>((int)StatusCode.InternalServerError, ex.Message);
        }
    }

    public async Task<DTO_Response<string>> RejectAsync(DTO_RejectRequest dto)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            var request = await _uow.Equipment.GetByIdWithDetailsAsync(dto.RequestId);
            if (request is null)
                return Fail<string>((int)StatusCode.NotFound, "Request not found.");

            if (request.RequesterId == dto.ApproverId)
                return Fail<string>((int)StatusCode.NotAcceptable, "You cannot reject your own request.");

            var pendingStep = await _uow.WorkFlow.GetPendingStepAsync(dto.RequestId);
            if (pendingStep is null || pendingStep.StepNumber != dto.Level)
                return Fail<string>((int)StatusCode.NotAcceptable, $"No pending step at level {dto.Level}.");

            pendingStep.Status     = StepRejected;
            pendingStep.ApproverId = dto.ApproverId;
            pendingStep.Comments   = dto.Reason;
            pendingStep.ActionDate = DateTime.UtcNow;
            _uow.WorkFlow.Update(pendingStep);

            // rejection status: level 1=7, 2=8, 3=9, 4=10
            request.Status          = StatusRejectedBase + (dto.Level - 1);
            request.RejectionReason = dto.Reason;
            _uow.Equipment.Update(request);

            await _uow.CommitAsync();

            await SendNotificationAsync(request.Id, dto.ApproverId, "Rejected", dto.Level);

            return Ok<string>("Request rejected.");
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return Fail<string>((int)StatusCode.InternalServerError, ex.Message);
        }
    }

    private async Task AdvanceWorkFlowAsync(EquipmentRequest request, int completedLevel, int approverId)
    {
        switch (completedLevel)
        {
            case 1:
                // Line Manager approved → IT Head
                request.Status = StatusPendingITHead;
                _uow.Equipment.Update(request);
                await _uow.WorkFlow.CreateWorkFlowStepAsync(new WorkFlowStep
                {
                    RequestId  = request.Id,
                    StepNumber = 2,
                    StepName   = "IT Department Head Approval",
                    Status     = StepPending,
                    CreatedDate = DateTime.UtcNow
                });
                break;

            case 2:
                // IT Head approved → Finance (if cost > threshold) or IT Ops
                if (request.EstimatedCost > FinanceThreshold)
                {
                    request.Status = StatusPendingFinance;
                    _uow.Equipment.Update(request);
                    await _uow.WorkFlow.CreateWorkFlowStepAsync(new WorkFlowStep
                    {
                        RequestId  = request.Id,
                        StepNumber = 3,
                        StepName   = "Finance Approval",
                        Status     = StepPending,
                        CreatedDate = DateTime.UtcNow
                    });
                }
                else
                {
                    goto case 3; // skip Finance
                }
                break;

            case 3:
                // Finance approved → IT Ops
                request.Status = StatusPendingITOps;
                _uow.Equipment.Update(request);
                await _uow.WorkFlow.CreateWorkFlowStepAsync(new WorkFlowStep
                {
                    RequestId  = request.Id,
                    StepNumber = 4,
                    StepName   = "IT Operations Fulfillment",
                    Status     = StepPending,
                    CreatedDate = DateTime.UtcNow
                });
                break;

            case 4:
                // IT Ops approved → fully done
                request.Status         = StatusFullyApproved;
                request.CompletionDate = DateTime.UtcNow;
                _uow.Equipment.Update(request);
                break;
        }
    }

    private async Task SendNotificationAsync(int requestId, int actorId, string action, int level)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");
            var payload = new
            {
                requestId,
                actorId,
                action,
                level,
                sentAt = DateTime.UtcNow
            };
            await client.PostAsJsonAsync("/notification/send", payload);
        }
        catch
        {
            // notification failure should not fail the main operation
        }
    }

    private static DTO_Response<T> Ok<T>(string message) => new()
    {
        code = (int)StatusCode.OK, error = false, message = message, data = new List<T>()
    };

    private static DTO_Response<T> Fail<T>(int code, string message) => new()
    {
        code = code, error = true, message = message, data = new List<T>()
    };
}
