using Restoran.Models;

namespace Restoran.Infrastructure.Security
{
    public static class RoleBridge
    {
        public static string GetSystemRoleCode(UserRole role) => role.ToString();

        public static bool TryMapRoleCode(string? roleCode, out UserRole role)
            => Enum.TryParse(roleCode, ignoreCase: true, out role);

        public static bool TryMapRole(Role? roleEntity, out UserRole role)
            => TryMapRoleCode(roleEntity?.Code, out role);

        public static UserRole ResolveRuntimeRole(User user)
            => TryMapRole(user.RoleEntity, out var mappedRole)
                ? mappedRole
                : user.Role;

        public static string ResolveSessionRole(User user)
            => ResolveRuntimeRole(user).ToString();
    }
}
