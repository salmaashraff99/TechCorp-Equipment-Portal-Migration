using System.Net;
using System.Net.Http;
using TechCorp.Shared.Enums;

namespace Equipment_Service.Tests.Services;

// Minimal fake HTTP factory so WorkFlow_Service notifications never throw
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
}

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(new FakeHttpMessageHandler()) { BaseAddress = new Uri("http://localhost") };
}

public class WorkFlowServiceTests
{
    private readonly MockUnitOfWork           _uow;
    private readonly WorkFlow_Service         _sut;
    private readonly EquipmentRequest_Service _requestService;

    public WorkFlowServiceTests()
    {
        _uow            = new MockUnitOfWork();
        _sut            = new WorkFlow_Service(_uow, new FakeHttpClientFactory());
        _requestService = new EquipmentRequest_Service(_uow);

        // Default mock state: pending request at level 1
        _uow.MockEquipment.RequestToReturn     = EquipmentMockData.PendingManagerRequest;
        _uow.MockWorkFlow.PendingStepToReturn  = EquipmentMockData.PendingStep1;
    }

    // ── Approve Tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_ValidStep_AdvancesToNextLevel()
    {
        var dto = EquipmentMockData.Level1ApprovalDto; // requestId=10, approverId=2, level=1

        var result = await _sut.ApproveAsync(dto);

        result.error.Should().BeFalse();
        result.code.Should().Be((int)StatusCode.OK);
        result.message.Should().Contain("approved successfully");

        // Step 1 must be marked approved
        _uow.MockWorkFlow.LastUpdated!.Status.Should().Be(3);
        _uow.MockWorkFlow.LastUpdated.ApproverId.Should().Be(2);

        // Next step (IT Head, level 2) must have been created
        _uow.MockWorkFlow.CreatedSteps.Should().ContainSingle(s => s.StepNumber == 2);
        _uow.MockWorkFlow.CreatedSteps[0].StepName.Should().Be("IT Department Head Approval");

        // Request status advanced to PendingITHead (3)
        _uow.MockEquipment.LastUpdated!.Status.Should().Be(3);
    }

    [Fact]
    public async Task Approve_Level4_CompletesRequest()
    {
        // Set up a request sitting at IT Ops (level 4)
        _uow.MockEquipment.RequestToReturn    = EquipmentMockData.ITOpsPendingRequest; // status=5
        _uow.MockWorkFlow.PendingStepToReturn = EquipmentMockData.PendingStep4;        // stepNumber=4

        var dto = EquipmentMockData.Level4ApprovalDto; // requestId=12, approverId=2, level=4

        var result = await _sut.ApproveAsync(dto);

        result.error.Should().BeFalse();
        result.code.Should().Be((int)StatusCode.OK);

        // No new step created at level 4 — it's the end
        _uow.MockWorkFlow.CreatedSteps.Should().BeEmpty();

        // Request must be fully approved (status=6) with a completion date
        _uow.MockEquipment.LastUpdated!.Status.Should().Be(6);
        _uow.MockEquipment.LastUpdated.CompletionDate.Should().NotBeNull();
    }

    // ── Reject Tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_WithComment_RejectsRequest()
    {
        var dto = EquipmentMockData.Level1RejectDto; // requestId=10, approverId=2, level=1, reason set

        var result = await _sut.RejectAsync(dto);

        result.error.Should().BeFalse();
        result.code.Should().Be((int)StatusCode.OK);
        result.message.Should().Contain("rejected");

        // Request status must be "Rejected by Line Manager" (7 = 7 + level(1) - 1)
        _uow.MockEquipment.LastUpdated!.Status.Should().Be(7);
        _uow.MockEquipment.LastUpdated.RejectionReason.Should().Be("Budget constraints this quarter.");

        // Step must be marked rejected (4)
        _uow.MockWorkFlow.LastUpdated!.Status.Should().Be(4);
    }

    [Fact]
    public async Task Reject_NoComment_ReturnsError()
    {
        var dto = new DTO_RejectRequest
        {
            RequestId  = 10,
            ApproverId = 2,
            Level      = 1,
            Reason     = "" // empty reason
        };

        var result = await _sut.RejectAsync(dto);

        result.error.Should().BeTrue();
        result.code.Should().Be((int)StatusCode.NotAcceptable);
        result.message.Should().Contain("reason is required");

        // No DB writes should have happened
        _uow.MockEquipment.LastUpdated.Should().BeNull();
        _uow.MockWorkFlow.LastUpdated.Should().BeNull();
    }

    // ── Guard Tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_RequesterApprovingOwn_ReturnsError()
    {
        // ApproverId matches the request's RequesterId (both = 1)
        var dto = new DTO_ApproveRequest
        {
            RequestId  = 10,
            ApproverId = 1, // same as PendingManagerRequest.RequesterId
            Level      = 1,
            Comments   = "Self-approve attempt"
        };

        var result = await _sut.ApproveAsync(dto);

        result.error.Should().BeTrue();
        result.code.Should().Be((int)StatusCode.NotAcceptable);
        result.message.Should().Contain("cannot approve your own request");

        // Nothing should have been written
        _uow.MockEquipment.LastUpdated.Should().BeNull();
        _uow.MockWorkFlow.LastUpdated.Should().BeNull();
    }

    // ── History Test ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistory_ReturnsAllSteps()
    {
        // WorkFlowHistory has 2 steps (one approved, one pending)
        _uow.MockWorkFlow.HistoryToReturn = EquipmentMockData.WorkFlowHistory;

        var result = await _requestService.GetHistoryAsync(10);

        result.error.Should().BeFalse();
        result.code.Should().Be((int)StatusCode.OK);
        result.data.Should().HaveCount(2);

        result.data[0].StepNumber.Should().Be(1);
        result.data[0].StatusName.Should().Be("Approved");
        result.data[0].ApproverName.Should().Be("Employee #2");

        result.data[1].StepNumber.Should().Be(2);
        result.data[1].StatusName.Should().Be("Pending");
        result.data[1].ApproverName.Should().Be("Pending");
    }
}
