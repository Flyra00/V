using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Tables.Services;
using Restoran.Filters;
using Restoran.Models;
using Restoran.ViewModels;

namespace Restoran.Controllers
{
    [RoleAuthorization(UserRole.Admin, UserRole.Supervisor)]
    public class TableController : Controller
    {
        private readonly ITableService _tableService;

        public TableController(ITableService tableService)
        {
            _tableService = tableService;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _tableService.GetManagementAsync());
        }

        public IActionResult Create()
        {
            return View(new TableFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TableFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _tableService.CreateAsync(model);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError(nameof(model.TableNumber), result.Message);
            }

            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var table = await _tableService.GetByIdAsync(id);
            if (table == null)
            {
                return NotFound();
            }

            return View(new TableFormViewModel
            {
                Id = table.Id,
                TableNumber = table.TableNumber,
                Capacity = table.Capacity,
                Status = table.TableSessions.Any() ? TableStatus.Occupied : table.Status,
                ExistingQrCodeUrl = string.IsNullOrWhiteSpace(table.QrCodeUrl)
                    ? $"/QR/Generate?tableId={table.Id}"
                    : table.QrCodeUrl,
                HasActiveSession = table.TableSessions.Any()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TableFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var result = await _tableService.UpdateAsync(id, model);
                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }

                if (result.IsNotFound)
                {
                    return NotFound();
                }

                ModelState.AddModelError(nameof(model.TableNumber), result.Message);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var result = await _tableService.DeactivateAsync(id);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(int id)
        {
            var result = await _tableService.ReactivateAsync(id);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _tableService.DeleteAsync(id);
            if (result.IsNotFound)
            {
                return NotFound();
            }

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
