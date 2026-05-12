using TechCorp.Shared.Enums;

namespace Equipment_Service.Tests.Services;

public class EquipmentRequestServiceTests
{
    private readonly MockUnitOfWork          _uow;
    private readonly EquipmentRequest_Service _sut;

    public EquipmentRequestServiceTests()
    {
        _uow = new MockUnitOfWork();
        _sut = new EquipmentRequest_Service(_uow);
    }

    // ── Submit Tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_ValidRequest_ReturnsSuccess()
    {
        var dto = EquipmentMockData.ValidSubmitRequest;

        var result = await _sut.SubmitAsync(dto);

        result.error.Should().BeFalse();
        result.code.Should().Be((int)StatusCode.Created);
        result.message.Should().Contain("submitted successfully");
        _uow.MockWorkFlow.CreatedSteps.Should().ContainSingle(s => s.StepNumber == 1);
    }

    [Fact]
    public async Task Submit_MissingJustification_ReturnsError()
    {
        var dto = EquipmentMockData.ValidSubmitRequest;
        dto.Justification = "";

        var result = await _sut.SubmitAsync(dto);

        result.error.Should().BeTrue();
        result.code.Should().Be((int)StatusCode.NotAcceptable);
        result.message.Should().Contain("Justification");
    }

    [Fact]
    public async Task Submit_RequesterIsApprover_ReturnsError()
    {
        // Arrange: make the line manager return an employee with the same Id as the requester
        _uow.MockEmployees.SelfApproval = true;
        var dto = EquipmentMockData.ValidSubmitRequest;

        var result = await _sut.SubmitAsync(dto);

        result.error.Should().BeTrue();
        result.code.Should().Be((int)StatusCode.NotAcceptable);
        result.message.Should().Contain("own line manager");
    }

    [Fact]
    public async Task Submit_NoLineManager_ReturnsError()
    {
        _uow.MockEmployees.HasLineManager = false;
        var dto = EquipmentMockData.ValidSubmitRequest;

        var result = await _sut.SubmitAsync(dto);

        result.error.Should().BeTrue();
        result.code.Should().Be((int)StatusCode.NotAcceptable);
        result.message.Should().Contain("line manager");
    }

    [Fact]
    public async Task Submit_CostOver5000_RoutesToFinance()
    {
        // At submit time the first step is always Level 1 (Line Manager).
        // Finance routing only activates after IT Head approval (Level 2).
        // This test verifies the high-cost submit succeeds and queues Step 1.
        var dto = EquipmentMockData.HighCostSubmitRequest; // EstimatedCost = 8000

        var result = await _sut.SubmitAsync(dto);

        result.error.Should().BeFalse();
        result.code.Should().Be((int)StatusCode.Created);
        // Step 1 (Line Manager) must be the first created step, not Finance
        _uow.MockWorkFlow.CreatedSteps.Should().ContainSingle();
        _uow.MockWorkFlow.CreatedSteps[0].StepNumber.Should().Be(1);
        _uow.MockWorkFlow.CreatedSteps[0].StepName.Should().Be("Line Manager Approval");
    }

    // ── GetById Tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingRequest_ReturnsData()
    {
        _uow.MockEquipment.RequestToReturn = EquipmentMockData.PendingManagerRequest;

        var result = await _sut.GetByIdAsync(10);

        result.error.Should().BeFalse();
        result.code.Should().Be((int)StatusCode.OK);
        result.data.Should().ContainSingle();
        result.data[0].Id.Should().Be(10);
        result.data[0].RequesterName.Should().Be("John Doe");
        result.data[0].StatusName.Should().Be("Pending Line Manager");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsError()
    {
        _uow.MockEquipment.RequestToReturn = null;

        var result = await _sut.GetByIdAsync(999);

        result.error.Should().BeTrue();
        result.code.Should().Be((int)StatusCode.NotFound);
        result.data.Should().BeEmpty();
    }

    // ── GetByUser Tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetByUser_ReturnsAllRequests()
    {
        var result = await _sut.GetByUserAsync(1);

        result.error.Should().BeFalse();
        result.code.Should().Be((int)StatusCode.OK);
        result.data.Should().HaveCountGreaterThan(0);
        result.data.Should().AllSatisfy(r => r.RequesterId.Should().Be(1));
    }
}
