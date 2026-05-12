using Microsoft.AspNetCore.Http;
using Restoran.Features.Auth.Dtos;
using Restoran.Models;

namespace Restoran.Features.Auth.Services
{
    public interface IAuthCookieService
    {
        Task SignInAsync(HttpContext httpContext, AuthenticatedSession session);
        Task SignOutAsync(HttpContext httpContext);
        int? GetUserId(HttpRequest request);
        AuthenticatedSession? GetAuthenticatedSession(HttpRequest request);
        UserRole? GetUserRole(HttpRequest request);
    }
}
