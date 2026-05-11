using Restoran.Features.Admin.Dtos;
using Restoran.Models;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Admin.Services
{
    public interface IAdminService
    {
        Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<RoleOption>> GetAssignableRolesAsync(CancellationToken cancellationToken = default);
        Task<Role?> GetRoleByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> CreateRoleAsync(RoleFormViewModel model, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateRoleAsync(int id, RoleFormViewModel model, CancellationToken cancellationToken = default);
        Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> CreateUserAsync(User user, string password, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateUserAsync(int id, User user, string? newPassword, CancellationToken cancellationToken = default);
        Task<OperationResult> DeactivateUserAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> ActivateUserAsync(int id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Promo>> GetPromosAsync(CancellationToken cancellationToken = default);
        Task<Promo?> GetPromoByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> CreatePromoAsync(PromoFormViewModel model, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdatePromoAsync(int id, PromoFormViewModel model, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PaymentMethodOption>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default);
        Task<PaymentMethodOption?> GetPaymentMethodByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<OperationResult> CreatePaymentMethodAsync(PaymentMethodFormViewModel model, CancellationToken cancellationToken = default);
        Task<OperationResult> UpdatePaymentMethodAsync(int id, PaymentMethodFormViewModel model, CancellationToken cancellationToken = default);
        Task<ChargeSettingsViewModel> GetChargeSettingsAsync(CancellationToken cancellationToken = default);
        Task<OperationResult> UpdateChargeSettingsAsync(ChargeSettingsViewModel model, CancellationToken cancellationToken = default);
    }
}
