using Microsoft.AspNetCore.Mvc;

namespace Cpp.Api.Controllers;

[ApiController]
[Route("api/agent")]
public class AgentController(IAssistantAgentService agentService) : ControllerBase {
  [HttpPost("messages")]
  public async Task<AssistantResponse> Message([FromBody] Cpp.Api.AssistantRequest request, CancellationToken ct) {
    return await agentService.ProcessAsync(request, ct);
  }

  [HttpGet("health")]
  public async Task<ActionResult<AssistantHealthResponse>> Health(CancellationToken ct) {
    var health = await agentService.CheckHealthAsync(ct);
    if (health.Status == "ok") return Ok(health);
    return StatusCode(503, health);
  }
}
