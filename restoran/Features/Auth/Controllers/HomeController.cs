using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Auth.Dtos;
using Restoran.Features.Auth.Services;
using Restoran.Features.Customer.Services;

namespace Restoran.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IAuthCookieService _authCookieService;
        private readonly ICustomerOrderContextService _customerOrderContextService;

        public HomeController(
            IAuthService authService,
            IAuthCookieService authCookieService,
            ICustomerOrderContextService customerOrderContextService)
        {
            _authService = authService;
            _authCookieService = authCookieService;
            _customerOrderContextService = customerOrderContextService;
        }

        public IActionResult Index()
        {
            // Redirect to Customer/Index as default landing page
            return RedirectToAction("Index", "Customer");
        }

        public IActionResult Login()
        {
            return View();
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            var result = await _authService.LoginStaffAsync(username, password);
            if (result.Succeeded && result.Session != null)
            {
                await _authCookieService.SignInAsync(HttpContext, result.Session);
                return LocalRedirect(result.RedirectUrl);
            }

            TempData["Error"] = result.Message;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MemberLogin(string email, string password, int? tableId = null)
        {
            var result = await _authService.LoginMemberOrStaffAsync(email, password);
            if (result.Succeeded && result.Session != null)
            {
                await _authCookieService.SignInAsync(HttpContext, result.Session);
                if (tableId.HasValue && tableId.Value > 0)
                {
                    _customerOrderContextService.SetActiveTableId(Response, tableId.Value);
                }
                return Json(new { success = true, redirectUrl = result.RedirectUrl, role = result.Role });
            }

            return Json(new { success = false, message = result.Message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MemberRegister([FromBody] MemberRegisterRequest model)
        {
            var result = await _authService.RegisterMemberAsync(model);
            if (result.Succeeded && result.Session != null)
            {
                await _authCookieService.SignInAsync(HttpContext, result.Session);
                if (model.TableId.HasValue && model.TableId.Value > 0)
                {
                    _customerOrderContextService.SetActiveTableId(Response, model.TableId.Value);
                }
                return Json(new { success = true, message = result.Message, redirectUrl = result.RedirectUrl });
            }

            return Json(new { success = false, message = result.Message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuestLogin(int tableId)
        {
            await _authCookieService.SignOutAsync(HttpContext);
            _customerOrderContextService.SetActiveTableId(Response, tableId);

            return Json(new { success = true, tableId = tableId });
        }

        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            _customerOrderContextService.Clear(Response);
            await _authCookieService.SignOutAsync(HttpContext);
            return RedirectToAction("Index", "Customer");
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
