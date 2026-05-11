using Microsoft.AspNetCore.Mvc;
using Restoran.Filters;
using Restoran.Models;
using Restoran.Features.Supervisor.Services;

namespace Restoran.Controllers
{
    [RoleAuthorization(UserRole.Supervisor, UserRole.Admin)]
    public class SupervisorController : Controller
    {
        private readonly ISupervisorService _supervisorService;

        public SupervisorController(ISupervisorService supervisorService)
        {
            _supervisorService = supervisorService;
        }

        public async Task<IActionResult> Assets()
        {
            return View(await _supervisorService.GetAssetsAsync());
        }

        public IActionResult CreateAsset()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAsset(Asset asset)
        {
            if (ModelState.IsValid)
            {
                var result = await _supervisorService.CreateAssetAsync(asset);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Assets));
                }

                ModelState.AddModelError(string.Empty, result.Message);
            }
            return View(asset);
        }

        public async Task<IActionResult> EditAsset(int id)
        {
            var asset = await _supervisorService.GetAssetByIdAsync(id);
            if (asset == null)
            {
                return NotFound();
            }
            return View(asset);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAsset(int id, Asset asset)
        {
            if (id != asset.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var result = await _supervisorService.UpdateAssetAsync(id, asset);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Assets));
                }

                if (result.IsNotFound)
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, result.Message);
            }
            return View(asset);
        }
    }
}
