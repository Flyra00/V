using Microsoft.AspNetCore.Http;
using Restoran.Features.Auth.Dtos;
using Restoran.Models;

namespace Restoran.Features.Auth.Services
{
    public interface IAuthCookieService
    {
        void SignIn(HttpResponse response, AuthenticatedSession session);
        void SignOut(HttpResponse response);
        int? GetUserId(HttpRequest request);
        AuthenticatedSession? GetAuthenticatedSession(HttpRequest request);
        UserRole? GetUserRole(HttpRequest request);
    }
}
