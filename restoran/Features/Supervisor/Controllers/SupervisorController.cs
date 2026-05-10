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

        public IActionResult Ingredients()
        {
            TempData["Success"] = "Manajemen bahan mentah sudah dipensiunkan. Anda diarahkan ke inventaris aset.";
            return RedirectToAction(nameof(Assets));
        }

        public IActionResult CreateIngredient()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateIngredient(Ingredient ingredient)
        {
            if (ModelState.IsValid)
            {
                var result = await _supervisorService.CreateIngredientAsync(ingredient);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Ingredients));
                }

                ModelState.AddModelError(string.Empty, result.Message);
            }
            return View(ingredient);
        }

        public async Task<IActionResult> EditIngredient(int id)
        {
            var ingredient = await _supervisorService.GetIngredientByIdAsync(id);
            if (ingredient == null)
            {
                return NotFound();
            }
            return View(ingredient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditIngredient(int id, Ingredient ingredient)
        {
            if (id != ingredient.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var result = await _supervisorService.UpdateIngredientAsync(id, ingredient);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Ingredients));
                }

                if (result.IsNotFound)
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, result.Message);
            }
            return View(ingredient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStock(int id, decimal amount)
        {
            var result = await _supervisorService.AddStockAsync(id, amount);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            return Json(new { success = result.Succeeded, newStock = result.Data, message = result.Message });
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
