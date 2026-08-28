using GenericInventory.Health.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GenericInventory.Health.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("health")]
public class HealthController : ControllerBase
{

    [HttpGet("liveness")]
    public HealthDto Post()
    {
        return new HealthDto
        {
            Status = "OK"
        };
    }

    [HttpGet("readiness")]
    [HttpPut("readiness")]
    public HealthDto Put()
    {
        return new HealthDto
        {
            Status = "OK"
        };
    }
}