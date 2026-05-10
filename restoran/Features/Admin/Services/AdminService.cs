using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Restoran.Features.Admin.Dtos;
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
                .Where(u => u.Role != UserRole.Member)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Username)
                .ToListAsync(cancellationToken);
        }

        public IReadOnlyList<RoleOption> GetAssignableRoles()
        {
            return Enum.GetValues(typeof(UserRole))
                .Cast<UserRole>()
                .Where(role => role != UserRole.Member)
                .Select(role => new RoleOption
                {
                    Value = role,
                    Text = role.ToString()
                })
                .ToList();
        }

        public async Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Users.FindAsync([id], cancellationToken);
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

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.CreatedAt = _dateTimeProvider.Now;
            user.IsActive = true;

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

            existingUser.Username = user.Username;
            existingUser.Email = user.Email;
            existingUser.Role = user.Role;
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
    }
}
