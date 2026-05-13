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
            var activePromos = await _cashierService.GetActivePromosAsync();
            return View(new KasirIndexViewModel
            {
                Transactions = transactions,
                ActivePromos = activePromos
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int transactionId, decimal? amountReceived)
        {
            var result = await _cashierService.ConfirmPaymentAsync(transactionId, amountReceived);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            var transactions = await _cashierService.GetTransactionsAsync();
            var transaction = transactions.FirstOrDefault(item => item.Id == transactionId);

            return Json(new
            {
                success = result.Succeeded,
                message = result.Message,
                paymentStatus = transaction?.Payment?.PaymentStatus.ToString() ?? string.Empty,
                midtransStatus = transaction?.Payment?.MidtransTransactionStatus ?? string.Empty,
                amountReceived = transaction?.Payment?.AmountReceived ?? 0,
                changeAmount = transaction?.Payment?.ChangeAmount ?? 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckMidtransStatus(int transactionId)
        {
            var result = await _cashierService.CheckMidtransStatusAsync(transactionId);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            var transactions = await _cashierService.GetTransactionsAsync();
            var transaction = transactions.FirstOrDefault(item => item.Id == transactionId);

            return Json(new
            {
                success = result.Succeeded,
                message = result.Message,
                paymentStatus = transaction?.Payment?.PaymentStatus.ToString() ?? string.Empty,
                midtransStatus = transaction?.Payment?.MidtransTransactionStatus ?? string.Empty,
                amountReceived = transaction?.Payment?.AmountReceived ?? 0,
                changeAmount = transaction?.Payment?.ChangeAmount ?? 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyPromo(int transactionId, int promoId)
        {
            var result = await _cashierService.ApplyPromoAsync(transactionId, promoId);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            var transaction = (await _cashierService.GetTransactionsAsync()).FirstOrDefault(item => item.Id == transactionId);

            return Json(new
            {
                success = result.Succeeded,
                message = result.Message,
                subtotal = transaction?.Subtotal ?? 0,
                discount = transaction?.Discount ?? 0,
                tax = transaction?.Tax ?? 0,
                serviceCharge = transaction?.ServiceCharge ?? 0,
                total = transaction?.Total ?? 0,
                promoName = transaction?.AppliedPromoName ?? string.Empty
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePromo(int transactionId)
        {
            var result = await _cashierService.RemovePromoAsync(transactionId);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            var transaction = (await _cashierService.GetTransactionsAsync()).FirstOrDefault(item => item.Id == transactionId);

            return Json(new
            {
                success = result.Succeeded,
                message = result.Message,
                subtotal = transaction?.Subtotal ?? 0,
                discount = transaction?.Discount ?? 0,
                tax = transaction?.Tax ?? 0,
                serviceCharge = transaction?.ServiceCharge ?? 0,
                total = transaction?.Total ?? 0,
                promoName = transaction?.AppliedPromoName ?? string.Empty
            });
        }

        [HttpGet]
        public async Task<IActionResult> PrintReceipt(int id)
        {
            var transaction = await _cashierService.GetTransactionForReceiptAsync(id);
            if (transaction == null)
            {
                return NotFound();
            }

            if (transaction.Payment == null || transaction.Payment.PaymentStatus != PaymentStatus.Paid)
            {
                if (TempData != null)
                {
                    TempData["Error"] = "Struk hanya dapat dicetak setelah pembayaran lunas.";
                }
                return RedirectToAction(nameof(Index));
            }

            return View("Receipt", transaction);
        }

        public async Task<IActionResult> POS()
        {
            var paymentMethods = (await _cashierService.GetAvailablePaymentMethodsAsync())
                .OrderBy(method => method.SortOrder)
                .ToList();

            return View(new PosPageViewModel
            {
                Products = await _cashierService.GetAvailableProductsAsync(),
                Tables = await _cashierService.GetAvailableTablesAsync(),
                PaymentMethods = BuildCashierPaymentMethods(paymentMethods),
                ActivePromos = await _cashierService.GetActivePromosAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePOSOrder([FromBody] CreatePosOrderRequest request)
        {
            var session = _authCookieService.GetAuthenticatedSession(Request);
            if (session == null || session.UserId <= 0)
            {
                return Unauthorized(new { success = false, message = "Sesi pengguna tidak valid. Silakan login kembali." });
            }

            var userId = session.UserId;
            var result = await _cashierService.CreatePosOrderAsync(request, userId);
            if (result.Succeeded && result.Data != null)
            {
                return Json(new { success = true, transactionId = result.Data.TransactionId, total = result.Data.Total });
            }

            return Json(new { success = false, message = result.Message });
        }

        private static IReadOnlyList<PaymentMethodSelectionViewModel> BuildCashierPaymentMethods(IReadOnlyList<PaymentMethodOption> paymentMethods)
        {
            var result = new List<PaymentMethodSelectionViewModel>();
            
            foreach (var method in paymentMethods)
            {
                if (method.LegacyMethod != PaymentMethod.Tunai)
                {
                    continue;
                }

                result.Add(new PaymentMethodSelectionViewModel
                {
                    Id = method.Id,
                    Code = method.Code,
                    DisplayName = method.DisplayName,
                    LegacyMethod = method.LegacyMethod,
                    IsCustomerFacing = method.IsCustomerFacing,
                    IsCashierFacing = method.IsCashierFacing
                });
            }

            return result;
        }
    }
}
