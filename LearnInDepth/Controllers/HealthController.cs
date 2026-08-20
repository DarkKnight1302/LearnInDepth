using Microsoft.AspNetCore.Mvc;

namespace LearnInDepth.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { status = "Healthy", service = "LearnInDepth", timestamp = DateTime.UtcNow });
        }
    }
}
