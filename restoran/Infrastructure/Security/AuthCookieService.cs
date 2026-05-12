using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Restoran.Features.Auth.Dtos;
using Restoran.Features.Auth.Services;
using Restoran.Models;

namespace Restoran.Infrastructure.Security
{
    public class AuthCookieService : IAuthCookieService
    {
        public const string MemberTypeClaimType = "member_type";

        private static readonly CookieOptions DefaultCookieOptions = new()
        {
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            HttpOnly = true
        };

        public async Task SignInAsync(HttpContext httpContext, AuthenticatedSession session)
        {
            ClearLegacyCookies(httpContext.Response);

            if (session.UserId <= 0 || string.IsNullOrWhiteSpace(session.Role))
            {
                return;
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                new(ClaimTypes.Name, session.Username),
                new(ClaimTypes.Role, session.Role)
            };

            if (session.IsMember && !string.IsNullOrWhiteSpace(session.MemberType))
            {
                claims.Add(new Claim(MemberTypeClaimType, session.MemberType));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    AllowRefresh = true
                });
        }

        public async Task SignOutAsync(HttpContext httpContext)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            ClearLegacyCookies(httpContext.Response);
        }

        public int? GetUserId(HttpRequest request)
        {
            var principal = request.HttpContext.User;
            var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdValue))
            {
                return null;
            }

            return int.TryParse(userIdValue, out var userId) ? userId : null;
        }

        public AuthenticatedSession? GetAuthenticatedSession(HttpRequest request)
        {
            var principal = request.HttpContext.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var userId = GetUserId(request);
            if (!userId.HasValue)
            {
                return null;
            }

            var role = principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var username = principal.Identity?.Name ?? string.Empty;
            var memberType = principal.FindFirstValue(MemberTypeClaimType);
            var isMember = string.Equals(role, UserRole.Member.ToString(), StringComparison.OrdinalIgnoreCase);

            return new AuthenticatedSession
            {
                UserId = userId.Value,
                Username = username,
                Role = role,
                IsMember = isMember,
                MemberType = memberType
            };
        }

        public UserRole? GetUserRole(HttpRequest request)
        {
            var session = GetAuthenticatedSession(request);
            if (session == null)
            {
                return null;
            }

            return Enum.TryParse<UserRole>(session.Role, out var role) ? role : null;
        }

        private static void ClearLegacyCookies(HttpResponse response)
        {
            response.Cookies.Delete("UserId", DefaultCookieOptions);
            response.Cookies.Delete("Username", DefaultCookieOptions);
            response.Cookies.Delete("Role", DefaultCookieOptions);
            response.Cookies.Delete("IsMember", DefaultCookieOptions);
            response.Cookies.Delete("MemberType", DefaultCookieOptions);
            response.Cookies.Delete("ActiveTransactionId", DefaultCookieOptions);
            response.Cookies.Delete("ActiveTableId", DefaultCookieOptions);
            response.Cookies.Delete("ActiveTrackingToken", DefaultCookieOptions);
        }
    }
}
