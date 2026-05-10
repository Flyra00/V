using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Auth.Services;
using Restoran.Features.Kasir.Dtos;
using Restoran.Features.Kasir.Services;
using Restoran.Filters;
using Restoran.Models;
using Restoran.ViewModels;

namespace Restoran.Controllers
{
    [RoleAuthorization(UserRole.Kasir, UserRole.Admin)]
    public class KasirController : Controller
    {
        private readonly ICashierService _cashierService;
        private readonly IAuthCookieService _authCookieService;

        public KasirController(ICashierService cashierService, IAuthCookieService authCookieService)
        {
            _cashierService = cashierService;
            _authCookieService = authCookieService;
        }

        public async Task<IActionResult> Index()
        {
            var transactions = await _cashierService.GetTransactionsAsync();
            return View(transactions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int transactionId)
        {
            var result = await _cashierService.ConfirmPaymentAsync(transactionId);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            return Json(new { success = result.Succeeded, message = result.Message });
        }

        public async Task<IActionResult> POS()
        {
            return View(new PosPageViewModel
            {
                Products = await _cashierService.GetAvailableProductsAsync(),
                Tables = await _cashierService.GetAvailableTablesAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePOSOrder([FromBody] CreatePosOrderRequest request)
        {
            var userId = _authCookieService.GetAuthenticatedSession(Request)?.UserId ?? 4;
            var result = await _cashierService.CreatePosOrderAsync(request, userId);
            if (result.Succeeded && result.Data != null)
            {
                return Json(new { success = true, transactionId = result.Data.TransactionId, total = result.Data.Total });
            }

            return Json(new { success = false, message = result.Message });
        }
    }
}
