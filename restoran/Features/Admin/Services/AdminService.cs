using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Features.Admin.Dtos;
using Restoran.Infrastructure.Security;
using Restoran.Models;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Results;
using Restoran.ViewModels;

namespace Restoran.Features.Admin.Services
{
    public class AdminService : IAdminService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;

        public AdminService(ApplicationDbContext context, IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AsNoTracking()
                .Include(u => u.RoleEntity)
                .Where(u => u.Role != UserRole.Member)
                .OrderBy(u => u.RoleEntity != null ? u.RoleEntity.SortOrder : int.MaxValue)
                .ThenBy(u => u.Username)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Roles
                .AsNoTracking()
                .OrderBy(role => role.SortOrder)
                .ThenBy(role => role.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<RoleOption>> GetAssignableRolesAsync(CancellationToken cancellationToken = default)
        {
            var roles = await _context.Roles
                .AsNoTracking()
                .Where(role => role.IsActive)
                .OrderBy(role => role.SortOrder)
                .ThenBy(role => role.Name)
                .ToListAsync(cancellationToken);

            return roles
                .Where(role =>
                    !string.Equals(role.Code, UserRole.Member.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    RoleBridge.TryMapRole(role, out _))
                .Select(role => new RoleOption
                {
                    Value = role.Id,
                    Code = role.Code,
                    Text = role.Name,
                    IsSystemRole = role.IsSystemRole
                })
                .ToList();
        }

        public async Task<Role?> GetRoleByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Roles.FindAsync([id], cancellationToken);
        }

        public async Task<OperationResult> CreateRoleAsync(RoleFormViewModel model, CancellationToken cancellationToken = default)
        {
            var validationError = await ValidateRoleAsync(model, null, cancellationToken);
            if (validationError != null)
            {
                return OperationResult.Failure(validationError);
            }

            _context.Roles.Add(new Role
            {
                Name = model.Name.Trim(),
                Code = model.Code.Trim(),
                IsSystemRole = false,
                IsActive = model.IsActive,
                SortOrder = model.SortOrder,
                CreatedAt = _dateTimeProvider.Now
            });

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Role berhasil dibuat");
        }

        public async Task<OperationResult> UpdateRoleAsync(int id, RoleFormViewModel model, CancellationToken cancellationToken = default)
        {
            if (id != model.Id)
            {
                return OperationResult.NotFound("Role tidak ditemukan");
            }

            var role = await _context.Roles.FindAsync([id], cancellationToken);
            if (role == null)
            {
                return OperationResult.NotFound("Role tidak ditemukan");
            }

            var validationError = await ValidateRoleAsync(model, id, cancellationToken);
            if (validationError != null)
            {
                return OperationResult.Failure(validationError);
            }

            role.Name = model.Name.Trim();
            role.SortOrder = model.SortOrder;

            if (!role.IsSystemRole)
            {
                role.Code = model.Code.Trim();
                role.IsActive = model.IsActive;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Role berhasil diperbarui");
        }

        public async Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Include(user => user.RoleEntity)
                .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
        }

        public async Task<OperationResult> CreateUserAsync(User user, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return OperationResult.Failure("Password wajib diisi");
            }

            if (await _context.Users.AnyAsync(u => u.Username == user.Username, cancellationToken))
            {
                return OperationResult.Failure("Username sudah digunakan");
            }

            if (await _context.Users.AnyAsync(u => u.Email == user.Email, cancellationToken))
            {
                return OperationResult.Failure("Email sudah digunakan");
            }

            var roleResolution = await ResolveManagedRoleAsync(user, cancellationToken);
            if (!roleResolution.Succeeded || roleResolution.Role == null || !roleResolution.RuntimeRole.HasValue)
            {
                return OperationResult.Failure(roleResolution.Message);
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.CreatedAt = _dateTimeProvider.Now;
            user.IsActive = true;
            user.RoleId = roleResolution.Role.Id;
            user.Role = roleResolution.RuntimeRole.Value;

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            return OperationResult.Success($"User {user.Username} berhasil dibuat");
        }

        public async Task<OperationResult> UpdateUserAsync(int id, User user, string? newPassword, CancellationToken cancellationToken = default)
        {
            if (id != user.Id)
            {
                return OperationResult.NotFound("User tidak ditemukan");
            }

            var existingUser = await _context.Users.FindAsync([id], cancellationToken);
            if (existingUser == null)
            {
                return OperationResult.NotFound("User tidak ditemukan");
            }

            if (existingUser.Username == "admin_V" && user.Username != "admin_V")
            {
                return OperationResult.Failure("Username admin_V tidak dapat diubah (user sistem)");
            }

            if (existingUser.Username != user.Username &&
                await _context.Users.AnyAsync(u => u.Username == user.Username && u.Id != id, cancellationToken))
            {
                return OperationResult.Failure("Username sudah digunakan");
            }

            if (existingUser.Email != user.Email &&
                await _context.Users.AnyAsync(u => u.Email == user.Email && u.Id != id, cancellationToken))
            {
                return OperationResult.Failure("Email sudah digunakan");
            }

            var roleResolution = await ResolveManagedRoleAsync(user, cancellationToken);
            if (!roleResolution.Succeeded || roleResolution.Role == null || !roleResolution.RuntimeRole.HasValue)
            {
                return OperationResult.Failure(roleResolution.Message);
            }

            existingUser.Username = user.Username;
            existingUser.Email = user.Email;
            existingUser.Role = roleResolution.RuntimeRole.Value;
            existingUser.RoleId = roleResolution.Role.Id;
            existingUser.IsActive = user.IsActive;

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success($"User {user.Username} berhasil diupdate");
        }

        public async Task<OperationResult> DeactivateUserAsync(int id, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users.FindAsync([id], cancellationToken);
            if (user == null)
            {
                return OperationResult.NotFound("User tidak ditemukan");
            }

            if (user.Username == "admin_V")
            {
                return OperationResult.Failure("User admin_V tidak dapat dinonaktifkan (user sistem)");
            }

            user.IsActive = false;
            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("User berhasil dinonaktifkan");
        }

        public async Task<OperationResult> ActivateUserAsync(int id, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users.FindAsync([id], cancellationToken);
            if (user == null)
            {
                return OperationResult.NotFound("User tidak ditemukan");
            }

            user.IsActive = true;
            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("User berhasil diaktifkan");
        }

        public async Task<IReadOnlyList<Promo>> GetPromosAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Promos
                .AsNoTracking()
                .OrderByDescending(promo => promo.IsActive)
                .ThenBy(promo => promo.StartsAt)
                .ThenBy(promo => promo.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<Promo?> GetPromoByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Promos.FindAsync([id], cancellationToken);
        }

        public async Task<OperationResult> CreatePromoAsync(PromoFormViewModel model, CancellationToken cancellationToken = default)
        {
            var validationError = await ValidatePromoAsync(model, null, cancellationToken);
            if (validationError != null)
            {
                return OperationResult.Failure(validationError);
            }

            _context.Promos.Add(new Promo
            {
                Name = model.Name.Trim(),
                PromoType = model.PromoType,
                DiscountValue = model.DiscountValue,
                MinimumPurchase = model.MinimumPurchase,
                StartsAt = model.StartsAt,
                EndsAt = model.EndsAt,
                IsActive = model.IsActive,
                CreatedAt = _dateTimeProvider.Now
            });

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Promo berhasil dibuat");
        }

        public async Task<OperationResult> UpdatePromoAsync(int id, PromoFormViewModel model, CancellationToken cancellationToken = default)
        {
            if (id != model.Id)
            {
                return OperationResult.NotFound("Promo tidak ditemukan");
            }

            var promo = await _context.Promos.FindAsync([id], cancellationToken);
            if (promo == null)
            {
                return OperationResult.NotFound("Promo tidak ditemukan");
            }

            var validationError = await ValidatePromoAsync(model, id, cancellationToken);
            if (validationError != null)
            {
                return OperationResult.Failure(validationError);
            }

            promo.Name = model.Name.Trim();
            promo.PromoType = model.PromoType;
            promo.DiscountValue = model.DiscountValue;
            promo.MinimumPurchase = model.MinimumPurchase;
            promo.StartsAt = model.StartsAt;
            promo.EndsAt = model.EndsAt;
            promo.IsActive = model.IsActive;

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Promo berhasil diperbarui");
        }

        public async Task<IReadOnlyList<PaymentMethodOption>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.PaymentMethodOptions
                .AsNoTracking()
                .OrderBy(option => option.SortOrder)
                .ThenBy(option => option.DisplayName)
                .ToListAsync(cancellationToken);
        }

        public async Task<PaymentMethodOption?> GetPaymentMethodByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.PaymentMethodOptions.FindAsync([id], cancellationToken);
        }

        public async Task<OperationResult> CreatePaymentMethodAsync(PaymentMethodFormViewModel model, CancellationToken cancellationToken = default)
        {
            var validationError = await ValidatePaymentMethodAsync(model, null, cancellationToken);
            if (validationError != null)
            {
                return OperationResult.Failure(validationError);
            }

            _context.PaymentMethodOptions.Add(new PaymentMethodOption
            {
                Code = model.Code.Trim(),
                DisplayName = model.DisplayName.Trim(),
                LegacyMethod = model.LegacyMethod,
                IsActive = model.IsActive,
                IsCustomerFacing = model.IsCustomerFacing,
                IsCashierFacing = model.IsCashierFacing,
                SortOrder = model.SortOrder,
                CreatedAt = _dateTimeProvider.Now
            });

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Metode pembayaran berhasil dibuat");
        }

        public async Task<OperationResult> UpdatePaymentMethodAsync(int id, PaymentMethodFormViewModel model, CancellationToken cancellationToken = default)
        {
            if (id != model.Id)
            {
                return OperationResult.NotFound("Metode pembayaran tidak ditemukan");
            }

            var paymentMethod = await _context.PaymentMethodOptions.FindAsync([id], cancellationToken);
            if (paymentMethod == null)
            {
                return OperationResult.NotFound("Metode pembayaran tidak ditemukan");
            }

            var validationError = await ValidatePaymentMethodAsync(model, id, cancellationToken);
            if (validationError != null)
            {
                return OperationResult.Failure(validationError);
            }

            paymentMethod.Code = model.Code.Trim();
            paymentMethod.DisplayName = model.DisplayName.Trim();
            paymentMethod.LegacyMethod = model.LegacyMethod;
            paymentMethod.IsActive = model.IsActive;
            paymentMethod.IsCustomerFacing = model.IsCustomerFacing;
            paymentMethod.IsCashierFacing = model.IsCashierFacing;
            paymentMethod.SortOrder = model.SortOrder;

            await _context.SaveChangesAsync(cancellationToken);
            return OperationResult.Success("Metode pembayaran berhasil diperbarui");
        }

        public async Task<ChargeSettingsViewModel> GetChargeSettingsAsync(CancellationToken cancellationToken = default)
        {
            var taxSetting = await _context.TaxSettings
                .OrderByDescending(setting => setting.IsActive)
                .ThenBy(setting => setting.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var serviceChargeSetting = await _context.ServiceChargeSettings
                .OrderByDescending(setting => setting.IsActive)
                .ThenBy(setting => setting.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return new ChargeSettingsViewModel
            {
                TaxSettingId = taxSetting?.Id,
                TaxName = taxSetting?.Name ?? "PPN",
                TaxPercentage = taxSetting?.Percentage ?? 10m,
                IsTaxActive = taxSetting?.IsActive ?? true,
                ServiceChargeSettingId = serviceChargeSetting?.Id,
                ServiceChargeName = serviceChargeSetting?.Name ?? "Service Charge",
                ServiceChargePercentage = serviceChargeSetting?.Percentage ?? 5m,
                IsServiceChargeActive = serviceChargeSetting?.IsActive ?? true
            };
        }

        public async Task<OperationResult> UpdateChargeSettingsAsync(ChargeSettingsViewModel model, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(model.TaxName) || string.IsNullOrWhiteSpace(model.ServiceChargeName))
            {
                return OperationResult.Failure("Nama pajak dan service charge wajib diisi");
            }

            var taxSetting = model.TaxSettingId.HasValue
                ? await _context.TaxSettings.FirstOrDefaultAsync(setting => setting.Id == model.TaxSettingId.Value, cancellationToken)
                : null;
            var serviceChargeSetting = model.ServiceChargeSettingId.HasValue
                ? await _context.ServiceChargeSettings.FirstOrDefaultAsync(setting => setting.Id == model.ServiceChargeSettingId.Value, cancellationToken)
                : null;

            if (taxSetting == null)
            {
                taxSetting = new TaxSetting
                {
                    CreatedAt = _dateTimeProvider.Now
                };
                _context.TaxSettings.Add(taxSetting);
            }

            if (serviceChargeSetting == null)
            {
                serviceChargeSetting = new ServiceChargeSetting
                {
                    CreatedAt = _dateTimeProvider.Now
                };
                _context.ServiceChargeSettings.Add(serviceChargeSetting);
            }

            taxSetting.Name = model.TaxName.Trim();
            taxSetting.Percentage = model.TaxPercentage;
            taxSetting.IsActive = model.IsTaxActive;

            serviceChargeSetting.Name = model.ServiceChargeName.Trim();
            serviceChargeSetting.Percentage = model.ServiceChargePercentage;
            serviceChargeSetting.IsActive = model.IsServiceChargeActive;

            var otherTaxSettings = await _context.TaxSettings
                .Where(setting => setting.Id != taxSetting.Id)
                .ToListAsync(cancellationToken);
            foreach (var setting in otherTaxSettings)
            {
                setting.IsActive = false;
            }

            var otherServiceChargeSettings = await _context.ServiceChargeSettings
                .Where(setting => setting.Id != serviceChargeSetting.Id)
                .ToListAsync(cancellationToken);
            foreach (var setting in otherServiceChargeSettings)
            {
                setting.IsActive = false;
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return OperationResult.Success("Pengaturan charge berhasil diperbarui");
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
            {
                return OperationResult.Failure("Nama pajak atau service charge sudah digunakan");
            }
        }

        private async Task<string?> ValidatePromoAsync(PromoFormViewModel model, int? existingPromoId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return "Nama promo wajib diisi";
            }

            if (model.DiscountValue < 0 || model.DiscountValue > 100)
            {
                return "Diskon promo harus di antara 0 sampai 100";
            }

            if (model.MinimumPurchase < 0)
            {
                return "Minimum pembelian tidak boleh negatif";
            }

            if (model.EndsAt <= model.StartsAt)
            {
                return "Periode promo tidak valid";
            }

            var normalizedName = model.Name.Trim();
            var duplicateExists = await _context.Promos.AnyAsync(
                promo => promo.Name == normalizedName && (!existingPromoId.HasValue || promo.Id != existingPromoId.Value),
                cancellationToken);

            if (duplicateExists)
            {
                return "Nama promo sudah digunakan";
            }

            return null;
        }

        private async Task<string?> ValidatePaymentMethodAsync(
            PaymentMethodFormViewModel model,
            int? existingPaymentMethodId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(model.Code))
            {
                return "Kode metode pembayaran wajib diisi";
            }

            if (string.IsNullOrWhiteSpace(model.DisplayName))
            {
                return "Nama tampilan metode pembayaran wajib diisi";
            }

            var normalizedCode = model.Code.Trim();
            var duplicateCode = await _context.PaymentMethodOptions.AnyAsync(
                option => option.Code == normalizedCode && (!existingPaymentMethodId.HasValue || option.Id != existingPaymentMethodId.Value),
                cancellationToken);

            if (duplicateCode)
            {
                return "Kode metode pembayaran sudah digunakan";
            }

            var duplicateLegacyMethod = await _context.PaymentMethodOptions.AnyAsync(
                option => option.LegacyMethod == model.LegacyMethod && (!existingPaymentMethodId.HasValue || option.Id != existingPaymentMethodId.Value),
                cancellationToken);

            if (duplicateLegacyMethod)
            {
                return "Tipe metode pembayaran legacy sudah dipakai";
            }

            if (model.IsActive && !model.IsCustomerFacing && !model.IsCashierFacing)
            {
                return "Metode pembayaran aktif harus tersedia untuk customer atau kasir";
            }

            if (!model.IsActive || !model.IsCustomerFacing)
            {
                var remainingCustomerFacing = await _context.PaymentMethodOptions.AnyAsync(
                    option => option.Id != (existingPaymentMethodId ?? 0) &&
                              option.IsActive &&
                              option.IsCustomerFacing,
                    cancellationToken);

                if (!remainingCustomerFacing)
                {
                    return "Minimal harus ada satu metode pembayaran aktif untuk customer";
                }
            }

            if (!model.IsActive || !model.IsCashierFacing)
            {
                var remainingCashierFacing = await _context.PaymentMethodOptions.AnyAsync(
                    option => option.Id != (existingPaymentMethodId ?? 0) &&
                              option.IsActive &&
                              option.IsCashierFacing,
                    cancellationToken);

                if (!remainingCashierFacing)
                {
                    return "Minimal harus ada satu metode pembayaran aktif untuk kasir";
                }
            }

            return null;
        }

        private async Task<string?> ValidateRoleAsync(
            RoleFormViewModel model,
            int? existingRoleId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return "Nama role wajib diisi";
            }

            if (string.IsNullOrWhiteSpace(model.Code))
            {
                return "Kode role wajib diisi";
            }

            var normalizedCode = model.Code.Trim();
            var normalizedCodeLower = normalizedCode.ToLower();
            var duplicateCode = await _context.Roles.AnyAsync(
                role => role.Code.ToLower() == normalizedCodeLower && (!existingRoleId.HasValue || role.Id != existingRoleId.Value),
                cancellationToken);

            if (duplicateCode)
            {
                return "Kode role sudah digunakan";
            }

            if (!existingRoleId.HasValue)
            {
                return null;
            }

            var existingRole = await _context.Roles
                .Include(role => role.Users)
                .FirstAsync(role => role.Id == existingRoleId.Value, cancellationToken);

            if (existingRole.IsSystemRole && !string.Equals(existingRole.Code, normalizedCode, StringComparison.OrdinalIgnoreCase))
            {
                return "Kode role sistem tidak dapat diubah";
            }

            if (existingRole.IsSystemRole && !model.IsActive)
            {
                return "Role sistem tidak dapat dinonaktifkan";
            }

            if (!model.IsActive && existingRole.Users.Count > 0)
            {
                return "Role yang masih dipakai user tidak dapat dinonaktifkan";
            }

            return null;
        }

        private async Task<ManagedRoleResolution> ResolveManagedRoleAsync(User user, CancellationToken cancellationToken)
        {
            if (user.RoleId.HasValue)
            {
                var role = await _context.Roles.FirstOrDefaultAsync(entity => entity.Id == user.RoleId.Value, cancellationToken);
                if (role == null)
                {
                    return ManagedRoleResolution.Failure("Role tidak ditemukan");
                }

                if (!role.IsActive)
                {
                    return ManagedRoleResolution.Failure("Role tidak aktif");
                }

                if (!RoleBridge.TryMapRole(role, out var runtimeRole))
                {
                    return ManagedRoleResolution.Failure("Role belum dapat dipakai untuk authorization runtime");
                }

                return ManagedRoleResolution.Success(role, runtimeRole);
            }

            if (await _context.Roles.AnyAsync(cancellationToken))
            {
                return ManagedRoleResolution.Failure("Role wajib dipilih");
            }

            return ManagedRoleResolution.Success(null, user.Role);
        }

        private sealed record ManagedRoleResolution(bool Succeeded, string Message, Role? Role, UserRole? RuntimeRole)
        {
            public static ManagedRoleResolution Success(Role? role, UserRole runtimeRole)
                => new(true, string.Empty, role, runtimeRole);

            public static ManagedRoleResolution Failure(string message)
                => new(false, message, null, null);
        }
    }
}
