using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Payments.Services;
using Restoran.Features.Tables.Services;

namespace Restoran.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/midtrans")]
    public class MidtransController : ControllerBase
    {
        private readonly IMidtransService _midtransService;
        private readonly ITableService _tableService;

        public MidtransController(IMidtransService midtransService, ITableService tableService)
        {
            _midtransService = midtransService;
            _tableService = tableService;
        }

        [HttpPost("notifications")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Notifications([FromBody] MidtransNotificationRequest notification, CancellationToken cancellationToken)
        {
            var result = await _midtransService.ProcessNotificationAsync(notification, cancellationToken);
            if (!result.Succeeded)
            {
                if (result.IsNotFound)
                {
                    return NotFound(new { success = false, message = result.Message });
                }

                if (string.Equals(result.Message, "Signature Midtrans tidak valid.", StringComparison.Ordinal))
                {
                    return Unauthorized(new { success = false, message = result.Message });
                }

                return BadRequest(new { success = false, message = result.Message });
            }

            if (result.Data?.PaymentStatus == Models.PaymentStatus.Paid)
            {
                await _tableService.TryCloseSessionForTransactionAsync(result.Data.TransactionId, cancellationToken);
            }

            return Ok(new { success = true, message = result.Message });
        }
    }
}
