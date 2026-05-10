using Microsoft.AspNetCore.Http;
using Restoran.Features.Auth.Dtos;
using Restoran.Features.Auth.Services;
using Restoran.Models;

namespace Restoran.Infrastructure.Security
{
    public class AuthCookieService : IAuthCookieService
    {
        private static readonly CookieOptions DefaultCookieOptions = new()
        {
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        };

        public void SignIn(HttpResponse response, AuthenticatedSession session)
        {
            response.Cookies.Append("UserId", session.UserId.ToString(), DefaultCookieOptions);
            response.Cookies.Append("Username", session.Username, DefaultCookieOptions);
            response.Cookies.Append("Role", session.Role, DefaultCookieOptions);
            response.Cookies.Append("IsMember", session.IsMember ? "true" : "false", DefaultCookieOptions);

            if (!string.IsNullOrWhiteSpace(session.MemberType))
            {
                response.Cookies.Append("MemberType", session.MemberType, DefaultCookieOptions);
            }
            else
            {
                response.Cookies.Delete("MemberType");
            }
        }

        public void SignOut(HttpResponse response)
        {
            response.Cookies.Delete("UserId");
            response.Cookies.Delete("Username");
            response.Cookies.Delete("Role");
            response.Cookies.Delete("IsMember");
            response.Cookies.Delete("MemberType");
        }

        public int? GetUserId(HttpRequest request)
        {
            if (!request.Cookies.TryGetValue("UserId", out var userIdValue))
            {
                return null;
            }

            return int.TryParse(userIdValue, out var userId) ? userId : null;
        }

        public AuthenticatedSession? GetAuthenticatedSession(HttpRequest request)
        {
            if (!request.Cookies.TryGetValue("Role", out var role) || string.IsNullOrWhiteSpace(role))
            {
                return null;
            }

            var userId = GetUserId(request);
            if (!userId.HasValue)
            {
                return null;
            }

            request.Cookies.TryGetValue("Username", out var username);
            request.Cookies.TryGetValue("IsMember", out var isMemberValue);
            request.Cookies.TryGetValue("MemberType", out var memberType);

            return new AuthenticatedSession
            {
                UserId = userId.Value,
                Username = username ?? string.Empty,
                Role = role,
                IsMember = string.Equals(isMemberValue, "true", StringComparison.OrdinalIgnoreCase),
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
    }
}
