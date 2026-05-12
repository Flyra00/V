using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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
        private readonly UserRole[] _allowedRoles;

        public RoleAuthorizationFilter(UserRole[] allowedRoles)
        {
            _allowedRoles = allowedRoles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                context.Result = new ChallengeResult(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var hasAllowedRole = _allowedRoles.Any(role => user.IsInRole(role.ToString()));
            if (!hasAllowedRole)
            {
                context.Result = new ForbidResult(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }
    }
}
