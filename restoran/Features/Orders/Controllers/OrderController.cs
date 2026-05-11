using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Auth.Services;
using Restoran.Features.Customer.Services;
using Restoran.Features.Orders.Services;
using Restoran.Models;
using Restoran.ViewModels;

namespace Restoran.Controllers
{
    public class OrderController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IAuthCookieService _authCookieService;
        private readonly ICustomerOrderContextService _customerOrderContextService;

        public OrderController(
            IOrderService orderService,
            IAuthCookieService authCookieService,
            ICustomerOrderContextService customerOrderContextService)
        {
            _orderService = orderService;
            _authCookieService = authCookieService;
            _customerOrderContextService = customerOrderContextService;
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
                _customerOrderContextService.SetActiveTransactionId(Response, result.Data.TransactionId);

                return Json(new
                {
                    success = true,
                    transactionId = result.Data.TransactionId,
                    transactionNumber = result.Data.TransactionNumber,
                    appliedPromoName = result.Data.AppliedPromoName,
                    discountAmount = result.Data.DiscountAmount,
                    trackingUrl = Url.Action(nameof(Tracking), new { id = result.Data.TransactionId })
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

        public IActionResult Confirmation(int id)
        {
            return RedirectToAction(nameof(Tracking), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Tracking(int? id)
        {
            var resolvedTransactionId = await ResolveAccessibleTransactionIdAsync();
            var effectiveTransactionId = id ?? resolvedTransactionId;

            if (!effectiveTransactionId.HasValue)
            {
                return View(new OrderTrackingViewModel
                {
                    IsEmptyState = true
                });
            }

            if (id.HasValue && !_customerOrderContextService.GetActiveTransactionId(Request).Equals(id.Value) && resolvedTransactionId != id.Value)
            {
                return NotFound();
            }

            var tracking = await _orderService.GetTrackingAsync(effectiveTransactionId.Value);
            if (tracking == null)
            {
                return NotFound();
            }

            _customerOrderContextService.SetActiveTransactionId(Response, tracking.TransactionId);
            if (tracking.TableId.HasValue)
            {
                _customerOrderContextService.SetActiveTableId(Response, tracking.TableId.Value);
            }

            return View(tracking);
        }

        [HttpGet]
        public async Task<IActionResult> TrackingStatus(int id)
        {
            var resolvedTransactionId = await ResolveAccessibleTransactionIdAsync();
            if (!_customerOrderContextService.GetActiveTransactionId(Request).Equals(id) && resolvedTransactionId != id)
            {
                return NotFound();
            }

            var status = await _orderService.GetTrackingStatusAsync(id);
            if (status == null)
            {
                return NotFound();
            }

            return Json(new
            {
                transactionId = status.TransactionId,
                orderStatus = status.OrderStatus.ToString(),
                paymentStatus = status.PaymentStatus.ToString(),
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

        private async Task<int?> ResolveAccessibleTransactionIdAsync()
        {
            var session = _authCookieService.GetAuthenticatedSession(Request);
            int? memberUserId = session?.IsMember == true ? session.UserId : null;

            return await _orderService.ResolveTrackingTransactionIdAsync(
                _customerOrderContextService.GetActiveTransactionId(Request),
                _customerOrderContextService.GetActiveTableId(Request),
                memberUserId);
        }
    }
}
