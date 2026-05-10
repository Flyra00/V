namespace Restoran.Features.Auth.Dtos
{
    public class AuthenticatedSession
    {
        public int UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public bool IsMember { get; init; }
        public string? MemberType { get; init; }
    }
}
