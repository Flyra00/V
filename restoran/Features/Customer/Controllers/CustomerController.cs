using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Customer.Services;
using Restoran.Features.Tables.Services;
using Restoran.Models;

namespace Restoran.Controllers
{
    [AllowAnonymous]
    public class CustomerController : Controller
    {
        private readonly ICustomerMenuService _customerMenuService;
        private readonly ITableService _tableService;

        public CustomerController(ICustomerMenuService customerMenuService, ITableService tableService)
        {
            _customerMenuService = customerMenuService;
            _tableService = tableService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Products = await _customerMenuService.GetAvailableProductsAsync();
            ViewBag.Tables = await _tableService.GetCustomerTableOptionsAsync();
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
