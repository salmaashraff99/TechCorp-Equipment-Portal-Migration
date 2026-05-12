using Equipment_Service.DTOs;
using Equipment_Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace Equipment_Service.Controllers;

[ApiController]
[Route("equipment")]
public class EquipmentRequestController : ControllerBase
{
    private readonly EquipmentRequest_Service _requestService;
    private readonly WorkFlow_Service _workFlowService;

    public EquipmentRequestController(EquipmentRequest_Service requestService, WorkFlow_Service workFlowService)
    {
        _requestService  = requestService;
        _workFlowService = workFlowService;
    }

    [HttpGet("request/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _requestService.GetByIdAsync(id);
        return StatusCode(result.code, result);
    }

    [HttpGet("request/user/{employeeId:int}")]
    public async Task<IActionResult> GetByUser(int employeeId)
    {
        var result = await _requestService.GetByUserAsync(employeeId);
        return StatusCode(result.code, result);
    }

    [HttpPost("request/submit")]
    public async Task<IActionResult> Submit([FromBody] DTO_SubmitRequest dto)
    {
        var result = await _requestService.SubmitAsync(dto);
        return StatusCode(result.code, result);
    }

    [HttpPost("request/approve")]
    public async Task<IActionResult> Approve([FromBody] DTO_ApproveRequest dto)
    {
        var result = await _workFlowService.ApproveAsync(dto);
        return StatusCode(result.code, result);
    }

    [HttpPost("request/reject")]
    public async Task<IActionResult> Reject([FromBody] DTO_RejectRequest dto)
    {
        var result = await _workFlowService.RejectAsync(dto);
        return StatusCode(result.code, result);
    }

    [HttpGet("request/{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id)
    {
        var result = await _requestService.GetHistoryAsync(id);
        return StatusCode(result.code, result);
    }

    [HttpGet("types")]
    public async Task<IActionResult> GetEquipmentTypes()
    {
        var result = await _requestService.GetEquipmentTypesAsync();
        return StatusCode(result.code, result);
    }
}
