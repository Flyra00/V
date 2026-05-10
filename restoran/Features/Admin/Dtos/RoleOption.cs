using Restoran.Models;

namespace Restoran.Features.Admin.Dtos
{
    public class RoleOption
    {
        public UserRole Value { get; init; }
        public string Text { get; init; } = string.Empty;
    }
}
