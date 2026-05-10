using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Kitchen.Services;
using Restoran.Filters;
using Restoran.Models;

namespace Restoran.Controllers
{
    [RoleAuthorization(UserRole.BagianMasak, UserRole.Admin)]
    public class KitchenController : Controller
    {
        private readonly IKitchenService _kitchenService;

        public KitchenController(IKitchenService kitchenService)
        {
            _kitchenService = kitchenService;
        }

        public async Task<IActionResult> Display()
        {
            return View((await _kitchenService.GetDisplayOrdersAsync()).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int transactionId, OrderStatus status)
        {
            var result = await _kitchenService.UpdateStatusAsync(transactionId, status);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            return Json(new { success = result.Succeeded, message = result.Message });
        }

        public async Task<IActionResult> PrintReceipt(int id)
        {
            var viewModel = await _kitchenService.GetPrintReceiptAsync(id);
            if (viewModel == null)
            {
                return NotFound();
            }

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetNewOrders()
        {
            return Json(new { newOrdersCount = await _kitchenService.GetNewOrdersCountAsync() });
        }
    }
}
