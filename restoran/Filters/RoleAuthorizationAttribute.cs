using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Restoran.Features.Auth.Services;
using Restoran.Models;

namespace Restoran.Filters
{
    public class RoleAuthorizationAttribute : TypeFilterAttribute
    {
        public RoleAuthorizationAttribute(params UserRole[] allowedRoles) : base(typeof(RoleAuthorizationFilter))
        {
            Arguments = new object[] { allowedRoles };
        }
    }

    public class RoleAuthorizationFilter : IAuthorizationFilter
    {
        private readonly IAuthCookieService _authCookieService;
        private readonly UserRole[] _allowedRoles;

        public RoleAuthorizationFilter(IAuthCookieService authCookieService, UserRole[] allowedRoles)
        {
            _authCookieService = authCookieService;
            _allowedRoles = allowedRoles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var session = _authCookieService.GetAuthenticatedSession(context.HttpContext.Request);
            var userRole = _authCookieService.GetUserRole(context.HttpContext.Request);

            if (session == null || !userRole.HasValue)
            {
                context.Result = new RedirectToActionResult("Login", "Home", null);
                return;
            }

            if (!_allowedRoles.Contains(userRole.Value))
            {
                if (userRole.Value == UserRole.Member)
                {
                    context.Result = new RedirectToActionResult("Index", "Customer", null);
                }
                else
                {
                    context.Result = new RedirectToActionResult("Login", "Home", null);
                }
            }
        }
    }
}
