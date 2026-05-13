using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Auth.Services;
using Restoran.Features.Customer.Services;
using Restoran.Features.Orders.Services;
using Restoran.Features.Tables.Services;
using Restoran.Models;
using Restoran.ViewModels;

namespace Restoran.Controllers
{
    public class OrderController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IAuthCookieService _authCookieService;
        private readonly ICustomerOrderContextService _customerOrderContextService;
        private readonly ITableService _tableService;

        public OrderController(
            IOrderService orderService,
            IAuthCookieService authCookieService,
            ICustomerOrderContextService customerOrderContextService,
            ITableService tableService)
        {
            _orderService = orderService;
            _authCookieService = authCookieService;
            _customerOrderContextService = customerOrderContextService;
            _tableService = tableService;
        }

        [HttpGet]
        public async Task<IActionResult> Menu(int? table, int? tableId)
        {
            var effectiveTableId = table ?? tableId;
            if (!effectiveTableId.HasValue || effectiveTableId.Value <= 0)
            {
                TempData["Error"] = "Meja ini sedang tidak tersedia. Silakan pilih meja lain.";
                return RedirectToAction("Index", "Customer");
            }

            if (!await _tableService.CanStartOrderAsync(effectiveTableId.Value))
            {
                TempData["Error"] = "Meja ini sedang tidak tersedia. Silakan pilih meja lain.";
                return RedirectToAction("Index", "Customer");
            }

            var viewModel = await _orderService.GetMenuAsync(effectiveTableId.Value);
            if (viewModel == null)
            {
                TempData["Error"] = "Meja ini sedang tidak tersedia. Silakan pilih meja lain.";
                return RedirectToAction("Index", "Customer");
            }

            var session = _authCookieService.GetAuthenticatedSession(Request);
            viewModel.IsMember = session?.IsMember == true;
            viewModel.MemberId = viewModel.IsMember ? session!.UserId : null;
            viewModel.MemberDiscountRate = ResolveMemberDiscountRate(session?.MemberType);
            viewModel.CustomerName = string.IsNullOrWhiteSpace(session?.Username) ? "Tamu" : session.Username;

            _customerOrderContextService.SetActiveTableId(Response, effectiveTableId.Value);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            var result = await _orderService.CreateOrderAsync(request);
            if (result.Succeeded && result.Data != null)
            {
                _customerOrderContextService.SetActiveTableId(Response, request.TableId);
                _customerOrderContextService.SetActiveTrackingToken(Response, result.Data.TrackingToken);

                return Json(new
                {
                    success = true,
                    transactionId = result.Data.TransactionId,
                    transactionNumber = result.Data.TransactionNumber,
                    trackingToken = result.Data.TrackingToken,
                    appliedPromoName = result.Data.AppliedPromoName,
                    discountAmount = result.Data.DiscountAmount,
                    trackingUrl = Url.Action(nameof(Tracking), new { token = result.Data.TrackingToken }),
                    isMidtransPayment = result.Data.IsMidtransPayment,
                    paymentRedirectUrl = result.Data.PaymentRedirectUrl,
                    snapToken = result.Data.SnapToken
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
        public async Task<IActionResult> UploadPaymentProof(string token, IFormFile paymentProof)
        {
            var result = await _orderService.UploadPaymentProofByTrackingTokenAsync(token, paymentProof);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            return Json(new { success = result.Succeeded, message = result.Message });
        }

        public IActionResult Confirmation(string? token = null, int? id = null)
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                return RedirectToAction(nameof(Tracking), new { token });
            }

            TempData["Error"] = "Tracking pesanan tidak ditemukan.";
            return RedirectToAction("Index", "Customer");
        }

        [HttpGet]
        public async Task<IActionResult> Tracking(string? token = null)
        {
            var requestedToken = NormalizeTrackingToken(token);
            var resolvedTrackingToken = await ResolveAccessibleTrackingTokenAsync(requestedToken);

            if (string.IsNullOrWhiteSpace(resolvedTrackingToken))
            {
                if (!string.IsNullOrWhiteSpace(requestedToken))
                {
                    return NotFound();
                }

                return View(new OrderTrackingViewModel
                {
                    IsEmptyState = true
                });
            }

            if (!string.IsNullOrWhiteSpace(requestedToken) && !string.Equals(requestedToken, resolvedTrackingToken, StringComparison.Ordinal))
            {
                return NotFound();
            }

            var tracking = await _orderService.GetTrackingByTokenAsync(resolvedTrackingToken);
            if (tracking == null)
            {
                return NotFound();
            }

            ViewData["CustomerStatusHref"] = Url.Action(nameof(Tracking), new { token = tracking.TrackingToken }) ?? "/Order/Tracking";
            _customerOrderContextService.SetActiveTrackingToken(Response, tracking.TrackingToken);
            if (tracking.TableId.HasValue)
            {
                _customerOrderContextService.SetActiveTableId(Response, tracking.TableId.Value);
            }

            return View(tracking);
        }

        [HttpGet]
        public async Task<IActionResult> TrackingStatus(string token)
        {
            var requestedToken = NormalizeTrackingToken(token);
            var resolvedTrackingToken = await ResolveAccessibleTrackingTokenAsync(requestedToken);
            if (string.IsNullOrWhiteSpace(requestedToken) ||
                string.IsNullOrWhiteSpace(resolvedTrackingToken) ||
                !string.Equals(requestedToken, resolvedTrackingToken, StringComparison.Ordinal))
            {
                return NotFound();
            }

            var status = await _orderService.GetTrackingStatusByTokenAsync(resolvedTrackingToken);
            if (status == null)
            {
                return NotFound();
            }

            return Json(new
            {
                transactionId = status.TransactionId,
                orderStatus = status.OrderStatus.ToString(),
                paymentStatus = status.PaymentStatus.ToString(),
                midtransStatus = status.MidtransTransactionStatus,
                paidAt = status.PaidAt?.ToString("O"),
                isTrackingFinal = status.IsTrackingFinal,
                refreshedAt = status.RefreshedAt.ToString("O"),
                items = status.Items.Select(item => new
                {
                    detailId = item.DetailId,
                    status = item.Status.ToString()
                })
            });
        }

        private async Task<string?> ResolveAccessibleTrackingTokenAsync(string? requestedToken = null)
        {
            var session = _authCookieService.GetAuthenticatedSession(Request);
            int? memberUserId = session?.IsMember == true ? session.UserId : null;

            return await _orderService.ResolveTrackingTokenAsync(
                requestedToken ?? _customerOrderContextService.GetActiveTrackingToken(Request),
                memberUserId);
        }

        private static decimal ResolveMemberDiscountRate(string? memberType)
        {
            return memberType switch
            {
                nameof(MemberType.Silver) => 5m,
                nameof(MemberType.Gold) => 10m,
                nameof(MemberType.Platinum) => 15m,
                _ => 0m
            };
        }

        private static string? NormalizeTrackingToken(string? token)
        {
            return string.IsNullOrWhiteSpace(token)
                ? null
                : token.Trim().ToLowerInvariant();
        }
    }
}
