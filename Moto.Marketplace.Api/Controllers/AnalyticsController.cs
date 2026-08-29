// Moto.Marketplace.Api/Controllers/AnalyticsController.cs
using Microsoft.AspNetCore.Mvc;
using Moto.Marketplace.Api.Services;

namespace Moto.Marketplace.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analytics;

        public AnalyticsController(IAnalyticsService analytics)
        {
            _analytics = analytics;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var data = await _analytics.GetDashboardAsync();
            return Ok(data);
        }

        [HttpGet("plugin/{id}")]
        public async Task<IActionResult> GetPluginAnalytics(string id)
        {
            var data = await _analytics.GetPluginAnalyticsAsync(id);
            return data != null ? Ok(data) : NotFound();
        }
    }
}
