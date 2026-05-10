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

        public IActionResult Create()
        {
            return View(new AdminUserFormViewModel
            {
                AvailableRoles = _adminService.GetAssignableRoles()
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

            model.AvailableRoles = _adminService.GetAssignableRoles();
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
                Role = user.Role,
                IsActive = user.IsActive,
                AvailableRoles = _adminService.GetAssignableRoles()
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

            model.AvailableRoles = _adminService.GetAssignableRoles();
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

            ModelState.AddModelError(string.Empty, message);
        }

        private static User MapToUser(AdminUserFormViewModel model)
        {
            return new User
            {
                Id = model.Id,
                Username = model.Username,
                Email = model.Email,
                Role = model.Role,
                IsActive = model.IsActive
            };
        }
    }
}
