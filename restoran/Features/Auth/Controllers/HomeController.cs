using Microsoft.AspNetCore.Mvc;
using Restoran.Features.Auth.Dtos;
using Restoran.Features.Auth.Services;

namespace Restoran.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IAuthCookieService _authCookieService;

        public HomeController(IAuthService authService, IAuthCookieService authCookieService)
        {
            _authService = authService;
            _authCookieService = authCookieService;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            var result = await _authService.LoginStaffAsync(username, password);
            if (result.Succeeded && result.Session != null)
            {
                _authCookieService.SignIn(Response, result.Session);
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
                _authCookieService.SignIn(Response, result.Session);
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
                _authCookieService.SignIn(Response, result.Session);
                return Json(new { success = true, message = result.Message, redirectUrl = result.RedirectUrl });
            }

            return Json(new { success = false, message = result.Message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GuestLogin(int tableId)
        {
            _authCookieService.SignIn(Response, _authService.CreateGuestSession());

            return Json(new { success = true, tableId = tableId });
        }

        public IActionResult Logout()
        {
            _authCookieService.SignOut(Response);
            return RedirectToAction("Index", "Customer");
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
