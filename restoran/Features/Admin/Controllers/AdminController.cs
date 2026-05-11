using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Admin.Services;
using Restoran.Filters;
using Restoran.Models;
using Restoran.ViewModels;

namespace Restoran.Controllers
{
    [RoleAuthorization(UserRole.Admin)]
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _adminService.GetUsersAsync());
        }

        public async Task<IActionResult> ChargeSettings()
        {
            return View(await _adminService.GetChargeSettingsAsync());
        }

        public async Task<IActionResult> Promos()
        {
            return View(await _adminService.GetPromosAsync());
        }

        public async Task<IActionResult> Roles()
        {
            return View(await _adminService.GetRolesAsync());
        }

        public IActionResult CreateRole()
        {
            return View(new RoleFormViewModel
            {
                IsActive = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(RoleFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _adminService.CreateRoleAsync(model);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Roles));
                }

                AddRoleErrors(result.Message);
            }

            return View(model);
        }

        public async Task<IActionResult> EditRole(int id)
        {
            var role = await _adminService.GetRoleByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }

            return View(new RoleFormViewModel
            {
                Id = role.Id,
                Name = role.Name,
                Code = role.Code,
                IsSystemRole = role.IsSystemRole,
                IsActive = role.IsActive,
                SortOrder = role.SortOrder
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(int id, RoleFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var result = await _adminService.UpdateRoleAsync(id, model);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Roles));
                }

                if (result.IsNotFound)
                {
                    return NotFound();
                }

                AddRoleErrors(result.Message);
            }

            return View(model);
        }

        public async Task<IActionResult> PaymentMethods()
        {
            return View(await _adminService.GetPaymentMethodsAsync());
        }

        public IActionResult CreatePaymentMethod()
        {
            return View(new PaymentMethodFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePaymentMethod(PaymentMethodFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _adminService.CreatePaymentMethodAsync(model);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(PaymentMethods));
                }

                AddPaymentMethodErrors(result.Message);
            }

            return View(model);
        }

        public async Task<IActionResult> EditPaymentMethod(int id)
        {
            var paymentMethod = await _adminService.GetPaymentMethodByIdAsync(id);
            if (paymentMethod == null)
            {
                return NotFound();
            }

            return View(new PaymentMethodFormViewModel
            {
                Id = paymentMethod.Id,
                Code = paymentMethod.Code,
                DisplayName = paymentMethod.DisplayName,
                LegacyMethod = paymentMethod.LegacyMethod,
                IsActive = paymentMethod.IsActive,
                IsCustomerFacing = paymentMethod.IsCustomerFacing,
                IsCashierFacing = paymentMethod.IsCashierFacing,
                SortOrder = paymentMethod.SortOrder
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPaymentMethod(int id, PaymentMethodFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var result = await _adminService.UpdatePaymentMethodAsync(id, model);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(PaymentMethods));
                }

                if (result.IsNotFound)
                {
                    return NotFound();
                }

                AddPaymentMethodErrors(result.Message);
            }

            return View(model);
        }

        public IActionResult CreatePromo()
        {
            return View(new PromoFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePromo(PromoFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _adminService.CreatePromoAsync(model);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Promos));
                }

                AddPromoErrors(result.Message);
            }

            return View(model);
        }

        public async Task<IActionResult> EditPromo(int id)
        {
            var promo = await _adminService.GetPromoByIdAsync(id);
            if (promo == null)
            {
                return NotFound();
            }

            return View(new PromoFormViewModel
            {
                Id = promo.Id,
                Name = promo.Name,
                PromoType = promo.PromoType,
                DiscountValue = promo.DiscountValue,
                MinimumPurchase = promo.MinimumPurchase,
                StartsAt = promo.StartsAt,
                EndsAt = promo.EndsAt,
                IsActive = promo.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPromo(int id, PromoFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var result = await _adminService.UpdatePromoAsync(id, model);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Promos));
                }

                if (result.IsNotFound)
                {
                    return NotFound();
                }

                AddPromoErrors(result.Message);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChargeSettings(ChargeSettingsViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _adminService.UpdateChargeSettingsAsync(model);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(ChargeSettings));
                }

                ModelState.AddModelError(string.Empty, result.Message);
            }

            return View(model);
        }

        public async Task<IActionResult> Create()
        {
            return View(new AdminUserFormViewModel
            {
                AvailableRoles = await _adminService.GetAssignableRolesAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminUserFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = MapToUser(model);
                var result = await _adminService.CreateUserAsync(user, model.Password);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }

                AddCreateErrors(result.Message);
            }

            model.AvailableRoles = await _adminService.GetAssignableRolesAsync();
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var user = await _adminService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return View(new AdminUserFormViewModel
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                RoleId = user.RoleId,
                SelectedRoleName = user.RoleEntity?.Name ?? user.Role.ToString(),
                IsActive = user.IsActive,
                AvailableRoles = await _adminService.GetAssignableRolesAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AdminUserFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var result = await _adminService.UpdateUserAsync(id, MapToUser(model), model.NewPassword);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }

                if (result.IsNotFound)
                {
                    return NotFound();
                }

                AddEditErrors(result.Message);
            }

            model.AvailableRoles = await _adminService.GetAssignableRolesAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _adminService.DeactivateUserAsync(id);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            return Json(new { success = result.Succeeded, message = result.Message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(int id)
        {
            var result = await _adminService.ActivateUserAsync(id);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            return Json(new { success = result.Succeeded, message = result.Message });
        }

        private void AddCreateErrors(string message)
        {
            if (message.Contains("Username", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Username", message);
                return;
            }

            if (message.Contains("Email", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Email", message);
                return;
            }

            if (message.Contains("Password", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("password", message);
                return;
            }

            if (message.Contains("Role", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("RoleId", message);
                return;
            }

            ModelState.AddModelError(string.Empty, message);
        }

        private void AddEditErrors(string message)
        {
            if (message.Contains("Username", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Username", message);
                return;
            }

            if (message.Contains("Email", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Email", message);
                return;
            }

            if (message.Contains("Role", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("RoleId", message);
                return;
            }

            ModelState.AddModelError(string.Empty, message);
        }

        private void AddPromoErrors(string message)
        {
            if (message.Contains("Nama promo", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Nama", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Name", message);
                return;
            }

            if (message.Contains("Diskon", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("DiscountValue", message);
                return;
            }

            if (message.Contains("Minimum pembelian", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("MinimumPurchase", message);
                return;
            }

            if (message.Contains("Periode", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("EndsAt", message);
                return;
            }

            ModelState.AddModelError(string.Empty, message);
        }

        private void AddPaymentMethodErrors(string message)
        {
            if (message.Contains("Kode", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Code", message);
                return;
            }

            if (message.Contains("Nama tampilan", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("DisplayName", message);
                return;
            }

            if (message.Contains("legacy", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Tipe metode", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("LegacyMethod", message);
                return;
            }

            if (message.Contains("customer", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("IsCustomerFacing", message);
                return;
            }

            if (message.Contains("kasir", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("IsCashierFacing", message);
                return;
            }

            ModelState.AddModelError(string.Empty, message);
        }

        private void AddRoleErrors(string message)
        {
            if (message.Contains("Nama role", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Name", message);
                return;
            }

            if (message.Contains("Kode role", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Kode", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Code", message);
                return;
            }

            if (message.Contains("aktif", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("dinonaktifkan", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("IsActive", message);
                return;
            }

            ModelState.AddModelError(string.Empty, message);
        }

        private static User MapToUser(AdminUserFormViewModel model)
        {
            return new User
            {
                Id = model.Id,
                Username = model.Username,
                Email = model.Email,
                RoleId = model.RoleId,
                IsActive = model.IsActive
            };
        }
    }
}
