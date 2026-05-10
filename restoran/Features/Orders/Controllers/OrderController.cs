using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Orders.Services;
using Restoran.Models;
using Restoran.ViewModels;

namespace Restoran.Controllers
{
    public class OrderController : Controller
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpGet]
        public async Task<IActionResult> Menu(int? table, int? tableId)
        {
            var effectiveTableId = table ?? tableId;
            if (!effectiveTableId.HasValue || effectiveTableId.Value <= 0)
            {
                return BadRequest("Meja tidak valid");
            }

            var viewModel = await _orderService.GetMenuAsync(effectiveTableId.Value);
            if (viewModel == null)
            {
                return NotFound("Meja tidak ditemukan");
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            var result = await _orderService.CreateOrderAsync(request);
            if (result.Succeeded && result.Data != null)
            {
                return Json(new
                {
                    success = true,
                    transactionId = result.Data.TransactionId,
                    transactionNumber = result.Data.TransactionNumber
                });
            }

            if (result.IsNotFound)
            {
                return NotFound(result.Message);
            }

            return Json(new { success = false, message = result.Message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPaymentProof(int transactionId, IFormFile paymentProof)
        {
            var result = await _orderService.UploadPaymentProofAsync(transactionId, paymentProof);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            return Json(new { success = result.Succeeded, message = result.Message });
        }

        public async Task<IActionResult> Confirmation(int id)
        {
            var transaction = await _orderService.GetConfirmationAsync(id);
            if (transaction == null)
            {
                return NotFound();
            }

            return View(transaction);
        }
    }
}
