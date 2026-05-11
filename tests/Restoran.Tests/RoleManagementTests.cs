using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Restoran.Data;
using Restoran.Features.Admin.Services;
using Restoran.Features.Auth.Services;
using Restoran.Infrastructure.Security;
using Restoran.Models;
using Restoran.ViewModels;

namespace Restoran.Tests;

public static class RoleManagementTests
{
    public static async Task SeedData_BackfillsUserRoleId_FromSystemRoles()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var services = new ServiceCollection()
            .AddSingleton(database.Options)
            .BuildServiceProvider();

        await SeedData.Initialize(services, new TestHostEnvironment
        {
            EnvironmentName = Environments.Development
        });

        await using var context = database.CreateContext();
        TestAssert.Equal(6, await context.Roles.CountAsync());
        TestAssert.True(await context.Users.AllAsync(user => user.RoleId != null));
    }

    public static async Task AdminService_UpdateRoleAsync_RejectsSystemRoleCodeChange()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using (var arrangeContext = database.CreateContext())
        {
            await TestRoleData.SeedDefaultRolesAsync(arrangeContext);
        }

        await using var context = database.CreateContext();
        var service = new AdminService(context, new FixedDateTimeProvider(DateTime.UtcNow));
        var adminRole = await context.Roles.SingleAsync(role => role.Code == UserRole.Admin.ToString());

        var result = await service.UpdateRoleAsync(adminRole.Id, new RoleFormViewModel
        {
            Id = adminRole.Id,
            Name = "Administrator Utama",
            Code = "ADMIN-RENAMED",
            IsSystemRole = true,
            IsActive = true,
            SortOrder = 1
        });

        TestAssert.False(result.Succeeded);
        TestAssert.Equal("Kode role sistem tidak dapat diubah", result.Message);
    }

    public static async Task AdminService_UpdateUserAsync_SyncsRoleId_AndLegacyRuntimeRole()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using (var arrangeContext = database.CreateContext())
        {
            await TestRoleData.SeedDefaultRolesAsync(arrangeContext);
            arrangeContext.Users.Add(new User
            {
                Id = 1,
                Username = "owner-user",
                Email = "owner@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret"),
                Role = UserRole.Owner,
                RoleId = await TestRoleData.GetRoleIdAsync(arrangeContext, UserRole.Owner),
                IsActive = true
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new AdminService(context, new FixedDateTimeProvider(DateTime.UtcNow));
        var cashierRoleId = await TestRoleData.GetRoleIdAsync(context, UserRole.Kasir);

        var result = await service.UpdateUserAsync(1, new User
        {
            Id = 1,
            Username = "owner-user",
            Email = "owner@test.local",
            RoleId = cashierRoleId,
            IsActive = true
        }, newPassword: null);

        TestAssert.True(result.Succeeded);

        var user = await context.Users.SingleAsync();
        TestAssert.Equal(UserRole.Kasir, user.Role);
        TestAssert.Equal(cashierRoleId, user.RoleId);
    }

    public static async Task AuthService_LoginStaffAsync_UsesRoleEntityBridge_ForSessionAndRedirect()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 11, 13, 0, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            await TestRoleData.SeedDefaultRolesAsync(arrangeContext);
            arrangeContext.Users.Add(new User
            {
                Username = "bridge-kasir",
                Email = "bridge-kasir@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Admin,
                RoleId = await TestRoleData.GetRoleIdAsync(arrangeContext, UserRole.Kasir),
                IsActive = true
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new AuthService(context, new FixedDateTimeProvider(now));

        var result = await service.LoginStaffAsync("bridge-kasir", "secret123");

        TestAssert.True(result.Succeeded);
        TestAssert.NotNull(result.Session);
        TestAssert.Equal("Kasir", result.Session!.Role);

        var resolvedUser = await context.Users
            .Include(user => user.RoleEntity)
            .SingleAsync(user => user.Username == "bridge-kasir");
        TestAssert.Equal(UserRole.Kasir, RoleBridge.ResolveRuntimeRole(resolvedUser));
    }
}
