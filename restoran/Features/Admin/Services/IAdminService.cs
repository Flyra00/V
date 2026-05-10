using Restoran.Features.Admin.Dtos;
using Restoran.Models;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Admin.Services
{
    public interface IAdminService
    {
        Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default);
        IReadOnlyList<RoleOption> GetAssignableRoles();
        Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> CreateUserAsync(User user, string password, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateUserAsync(int id, User user, string? newPassword, CancellationToken cancellationToken = default);
        Task<OperationResult> DeactivateUserAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> ActivateUserAsync(int id, CancellationToken cancellationToken = default);
        Task<ChargeSettingsViewModel> GetChargeSettingsAsync(CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateChargeSettingsAsync(ChargeSettingsViewModel model, CancellationToken cancellationToken = default);
    }
}
