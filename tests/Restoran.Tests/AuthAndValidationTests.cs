using Microsoft.EntityFrameworkCore;
using Restoran.Features.Admin.Services;
using Restoran.Features.Auth.Services;
using Restoran.Features.Catalog.Services;
using Restoran.Models;

namespace Restoran.Tests;

public static class AuthAndValidationTests
{
    public static async Task LoginStaffAsync_ReturnsSuccess_AndUpdatesLastLogin()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var now = new DateTime(2026, 5, 9, 10, 30, 0, DateTimeKind.Utc);

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Users.Add(new User
            {
                Username = "kasir",
                Email = "kasir@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Kasir,
                IsActive = true
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new AuthService(context, new FixedDateTimeProvider(now));

        var result = await service.LoginStaffAsync("kasir", "secret123");

        TestAssert.True(result.Succeeded);
        TestAssert.NotNull(result.Session);
        TestAssert.Equal("/Kasir", result.RedirectUrl);
        TestAssert.Equal("Kasir", result.Session!.Role);

        var updatedUser = await context.Users.SingleAsync();
        TestAssert.Equal(now, updatedUser.LastLogin);
    }

    public static async Task LoginStaffAsync_ReturnsFailure_WhenPasswordInvalid()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        await using (var arrangeContext = database.CreateContext())
        {
            arrangeContext.Users.Add(new User
            {
                Username = "admin",
                Email = "admin@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-password"),
                Role = UserRole.Admin,
                IsActive = true
            });
            await arrangeContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var service = new AuthService(context, new FixedDateTimeProvider(DateTime.UtcNow));

        var result = await service.LoginStaffAsync("admin", "wrong-password");

        TestAssert.False(result.Succeeded);
        TestAssert.Equal("Username atau password salah", result.Message);
        TestAssert.Null(result.Session);
    }

    public static async Task CategoryService_CreateCategoryAsync_RejectsDuplicateName_CaseInsensitive()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();

        context.Categories.Add(new Category { Name = "Makanan" });
        await context.SaveChangesAsync();

        var service = new CategoryService(context, new FixedDateTimeProvider(DateTime.UtcNow));

        var result = await service.CreateCategoryAsync(new Category { Name = "makanan" });

        TestAssert.False(result.Succeeded);
        TestAssert.Equal("Nama kategori sudah digunakan", result.Message);
    }

    public static async Task AdminService_CreateUserAsync_RejectsDuplicateUsername()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();

        context.Users.Add(new User
        {
            Username = "supervisor",
            Email = "existing@test.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret"),
            Role = UserRole.Supervisor,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new AdminService(context, new FixedDateTimeProvider(DateTime.UtcNow));

        var result = await service.CreateUserAsync(new User
        {
            Username = "supervisor",
            Email = "new@test.local",
            Role = UserRole.Kasir
        }, "password123");

        TestAssert.False(result.Succeeded);
        TestAssert.Equal("Username sudah digunakan", result.Message);
    }

    public static async Task AdminService_CreateUserAsync_RejectsDuplicateEmail()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();

        context.Users.Add(new User
        {
            Username = "owner",
            Email = "owner@test.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret"),
            Role = UserRole.Owner,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new AdminService(context, new FixedDateTimeProvider(DateTime.UtcNow));

        var result = await service.CreateUserAsync(new User
        {
            Username = "new-owner",
            Email = "owner@test.local",
            Role = UserRole.Owner
        }, "password123");

        TestAssert.False(result.Succeeded);
        TestAssert.Equal("Email sudah digunakan", result.Message);
    }
}
