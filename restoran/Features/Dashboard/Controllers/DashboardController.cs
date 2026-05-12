using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Auth.Services;
using Restoran.Features.Dashboard.Services;
using Restoran.Filters;
using Restoran.Models;
using Restoran.Shared.Abstractions;
using Restoran.ViewModels;

namespace Restoran.Controllers
{
    [RoleAuthorization(UserRole.Owner, UserRole.Admin)]
    public class DashboardController : Controller
    {
        private readonly IDashboardService _dashboardService;
        private readonly IProductImageStorage _productImageStorage;
        private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
        private const long MaxImageFileSize = 2 * 1024 * 1024;

        public DashboardController(IDashboardService dashboardService, IProductImageStorage productImageStorage)
        {
            _dashboardService = dashboardService;
            _productImageStorage = productImageStorage;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _dashboardService.GetDashboardAsync());
        }

        [RoleAuthorization(UserRole.Admin)]
        public async Task<IActionResult> ManageProducts()
        {
            return View(new ProductManagementViewModel
            {
                Products = await _dashboardService.GetProductsAsync(),
                Categories = await _dashboardService.GetCategoriesAsync()
            });
        }

        [RoleAuthorization(UserRole.Admin)]
        public async Task<IActionResult> CreateProduct()
        {
            return View(await BuildProductFormAsync());
        }

        [RoleAuthorization(UserRole.Admin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(ProductFormViewModel model)
        {
            await ValidateAndPopulateImageAsync(model);

            if (ModelState.IsValid)
            {
                var product = MapToProduct(model);
                var result = await _dashboardService.CreateProductAsync(product);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(ManageProducts));
                }

                ModelState.AddModelError(string.Empty, result.Message);
            }

            model.Categories = await _dashboardService.GetCategoriesAsync();
            return View(model);
        }

        [RoleAuthorization(UserRole.Admin)]
        public async Task<IActionResult> EditProduct(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _dashboardService.GetProductByIdAsync(id.Value);
            if (product == null)
            {
                return NotFound();
            }

            return View(await BuildProductFormAsync(product));
        }

        [RoleAuthorization(UserRole.Admin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, ProductFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            await ValidateAndPopulateImageAsync(model);

            if (ModelState.IsValid)
            {
                var product = MapToProduct(model);
                var result = await _dashboardService.UpdateProductAsync(id, product);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(ManageProducts));
                }

                if (result.IsNotFound)
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, result.Message);
            }

            model.Categories = await _dashboardService.GetCategoriesAsync();
            return View(model);
        }

        [RoleAuthorization(UserRole.Admin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var result = await _dashboardService.DeleteProductAsync(id);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(ManageProducts));
        }

        public async Task<IActionResult> RevenueReport(DateTime? fromDate, DateTime? toDate)
        {
            return View(await _dashboardService.GetRevenueReportAsync(fromDate, toDate));
        }

        public async Task<IActionResult> StockReport()
        {
            return View(await _dashboardService.GetStockReportAsync());
        }

        private async Task<ProductFormViewModel> BuildProductFormAsync(Product? product = null)
        {
            return new ProductFormViewModel
            {
                Id = product?.Id ?? 0,
                Name = product?.Name ?? string.Empty,
                Description = product?.Description ?? string.Empty,
                Price = product?.Price ?? 0,
                CategoryId = product?.CategoryId ?? 0,
                MemberDiscountPercentage = product?.MemberDiscountPercentage ?? 0,
                IsAvailable = product?.IsAvailable ?? true,
                ExistingImageUrl = product?.ImageUrl ?? string.Empty,
                CreatedAt = product?.CreatedAt ?? DateTime.Now,
                Categories = await _dashboardService.GetCategoriesAsync()
            };
        }

        private async Task ValidateAndPopulateImageAsync(ProductFormViewModel model)
        {
            if (model.ImageFile == null || model.ImageFile.Length == 0)
            {
                return;
            }

            if (model.ImageFile.Length > MaxImageFileSize)
            {
                ModelState.AddModelError(nameof(model.ImageFile), "Ukuran gambar maksimal 2MB");
                return;
            }

            var extension = Path.GetExtension(model.ImageFile.FileName);
            if (!AllowedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.ImageFile), "Format gambar harus JPG, PNG, GIF, atau WEBP");
                return;
            }

            model.ExistingImageUrl = await _productImageStorage.SaveAsync(model.ImageFile);
        }

        private static Product MapToProduct(ProductFormViewModel model)
        {
            return new Product
            {
                Id = model.Id,
                Name = model.Name,
                Description = model.Description,
                Price = model.Price,
                CategoryId = model.CategoryId,
                MemberDiscountPercentage = model.MemberDiscountPercentage,
                IsAvailable = model.IsAvailable,
                ImageUrl = model.ExistingImageUrl,
                CreatedAt = model.CreatedAt
            };
        }
    }
}
