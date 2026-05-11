namespace Restoran.Features.Admin.Dtos
{
    public class RoleOption
    {
        public int Value { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public bool IsSystemRole { get; init; }
    }
}
