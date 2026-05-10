using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Auth.Services;
using Restoran.Features.Supervisor.Services;
using Restoran.Filters;
using Restoran.Models;
using Restoran.ViewModels;

namespace Restoran.Controllers
{
    [RoleAuthorization(UserRole.Supervisor, UserRole.Admin)]
    public class AssetLogController : Controller
    {
        private readonly IAssetLogService _assetLogService;
        private readonly IAuthCookieService _authCookieService;

        public AssetLogController(IAssetLogService assetLogService, IAuthCookieService authCookieService)
        {
            _assetLogService = assetLogService;
            _authCookieService = authCookieService;
        }

        public async Task<IActionResult> Create()
        {
            return View(new AssetLogViewModel
            {
                AvailableAssets = await _assetLogService.GetAvailableAssetsAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AssetLogViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = _authCookieService.GetAuthenticatedSession(Request)?.UserId ?? 1;
                var result = await _assetLogService.CreateAsync(model, userId);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction("Index", "Home");
                }

                if (result.Message.Contains("Kuantitas", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("Quantity", result.Message);
                }
                else if (result.Message.Contains("Aset", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(string.Empty, result.Message);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, result.Message);
                }
            }

            model.AvailableAssets = await _assetLogService.GetAvailableAssetsAsync();
            return View(model);
        }

        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, LogStatus? status)
        {
            return View(await _assetLogService.GetLogsAsync(fromDate, toDate, status));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var approverUserId = _authCookieService.GetAuthenticatedSession(Request)?.UserId ?? 1;
            var result = await _assetLogService.ApproveAsync(id, approverUserId);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            return Json(new { success = result.Succeeded, message = result.Message });
        }

        public async Task<IActionResult> Report()
        {
            return View(await _assetLogService.GetReportAsync());
        }
    }
}
