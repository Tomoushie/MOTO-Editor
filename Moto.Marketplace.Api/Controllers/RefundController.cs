// Moto.Marketplace.Api/Controllers/RefundController.cs
using Microsoft.AspNetCore.Mvc;
using Moto.Marketplace.Api.Services;

namespace Moto.Marketplace.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class RefundController : ControllerBase
    {
        private readonly IRefundService _refundService;

        public RefundController(IRefundService refundService)
        {
            _refundService = refundService;
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestRefund([FromBody] RefundRequestBody request)
        {
            var result = await _refundService.RequestRefundAsync(
                request.LicenseKey, request.UserId, request.Reason);

            return result.Success
                ? Ok(new { message = "Demande de remboursement soumise" })
                : BadRequest(new { error = result.Error });
        }

        [HttpPost("process/{purchaseId}")]
        public async Task<IActionResult> ProcessRefund(string purchaseId)
        {
            var result = await _refundService.ProcessRefundAsync(purchaseId);
            return result.Success
                ? Ok(new { refundId = result.RefundId, amount = result.RefundedAmount })
                : BadRequest(new { error = result.Error });
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingRefunds()
        {
            var refunds = await _refundService.GetPendingRefundsAsync();
            return Ok(refunds);
        }
    }

    public record RefundRequestBody(string LicenseKey, string UserId, string Reason);
}
