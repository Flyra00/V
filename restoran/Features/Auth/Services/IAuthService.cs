using Restoran.Features.Auth.Dtos;

namespace Restoran.Features.Auth.Services
{
    public interface IAuthService
    {
        Task<AuthResult> LoginStaffAsync(string username, string password, CancellationToken cancellationToken = default);
        Task<AuthResult> LoginMemberOrStaffAsync(string identifier, string password, CancellationToken cancellationToken = default);
        Task<AuthResult> RegisterMemberAsync(MemberRegisterRequest request, CancellationToken cancellationToken = default);
        AuthenticatedSession CreateGuestSession();
    }
}
