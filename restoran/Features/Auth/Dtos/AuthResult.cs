namespace Restoran.Features.Auth.Dtos
{
    public class AuthResult
    {
        public bool Succeeded { get; init; }
        public string Message { get; init; } = string.Empty;
        public string RedirectUrl { get; init; } = "/Customer/Index";
        public string Role { get; init; } = string.Empty;
        public AuthenticatedSession? Session { get; init; }

        public static AuthResult Success(AuthenticatedSession session, string redirectUrl, string role, string message = "")
            => new()
            {
                Succeeded = true,
                Session = session,
                RedirectUrl = redirectUrl,
                Role = role,
                Message = message
            };

        public static AuthResult Failure(string message)
            => new() { Succeeded = false, Message = message };
    }
}
