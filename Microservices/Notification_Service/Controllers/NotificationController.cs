using Microsoft.AspNetCore.Mvc;
using Notification_Service.DTOs;
using Notification_Service.Services;

namespace Notification_Service.Controllers;

[ApiController]
[Route("notification")]
public class NotificationController : ControllerBase
{
    private readonly NotificationService _service;

    public NotificationController(NotificationService service) => _service = service;

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] DTO_SendNotification dto)
    {
        var result = await _service.SendAsync(dto);
        return StatusCode(result.code, result);
    }

    [HttpGet("{requestId:int}")]
    public async Task<IActionResult> GetByRequest(int requestId)
    {
        var result = await _service.GetByRequestIdAsync(requestId);
        return StatusCode(result.code, result);
    }

    [HttpPost("retry-failed")]
    public async Task<IActionResult> RetryFailed()
    {
        var result = await _service.RetryFailedAsync();
        return StatusCode(result.code, result);
    }
}
