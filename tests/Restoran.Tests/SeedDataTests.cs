using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Restoran.Data;
using Restoran.Models;

namespace Restoran.Tests;

public static class SeedDataTests
{
    public static async Task InitializeAsync_SeedsDevelopmentDemoData_AndIsIdempotent()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var services = new ServiceCollection()
            .AddSingleton(database.Options)
            .BuildServiceProvider();

        var hostEnvironment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Development
        };

        await SeedData.Initialize(services, hostEnvironment);
        await AssertDevelopmentSeedAsync(database);

        await using (var firstRunContext = database.CreateContext())
        {
            var countsAfterFirstRun = await CaptureCountsAsync(firstRunContext);

            await SeedData.Initialize(services, hostEnvironment);

            await using var secondRunContext = database.CreateContext();
            var countsAfterSecondRun = await CaptureCountsAsync(secondRunContext);

            TestAssert.Equal(countsAfterFirstRun.Users, countsAfterSecondRun.Users);
            TestAssert.Equal(countsAfterFirstRun.Members, countsAfterSecondRun.Members);
            TestAssert.Equal(countsAfterFirstRun.Transactions, countsAfterSecondRun.Transactions);
            TestAssert.Equal(countsAfterFirstRun.TransactionDetails, countsAfterSecondRun.TransactionDetails);
            TestAssert.Equal(countsAfterFirstRun.Payments, countsAfterSecondRun.Payments);
            TestAssert.Equal(countsAfterFirstRun.PaymentMethods, countsAfterSecondRun.PaymentMethods);
            TestAssert.Equal(countsAfterFirstRun.Roles, countsAfterSecondRun.Roles);
            TestAssert.Equal(countsAfterFirstRun.AssetLogs, countsAfterSecondRun.AssetLogs);
            TestAssert.Equal(countsAfterFirstRun.Notifications, countsAfterSecondRun.Notifications);
            TestAssert.Equal(countsAfterFirstRun.TableSessions, countsAfterSecondRun.TableSessions);
        }
    }

    public static async Task InitializeAsync_SkipsDemoData_OutsideDevelopment()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var services = new ServiceCollection()
            .AddSingleton(database.Options)
            .BuildServiceProvider();

        await SeedData.Initialize(services, new TestHostEnvironment
        {
            EnvironmentName = Environments.Production
        });

        await using var context = database.CreateContext();
        TestAssert.Equal(4, await context.Categories.CountAsync());
        TestAssert.Equal(8, await context.Tables.CountAsync());
        TestAssert.Equal(12, await context.Products.CountAsync());
        TestAssert.Equal(4, await context.PaymentMethodOptions.CountAsync());
        TestAssert.Equal(6, await context.Roles.CountAsync());
        TestAssert.Equal(0, await context.Users.CountAsync());
        TestAssert.Equal(0, await context.Members.CountAsync());
        TestAssert.Equal(0, await context.Transactions.CountAsync());
        TestAssert.Equal(0, await context.Payments.CountAsync());
        TestAssert.Equal(0, await context.TableSessions.CountAsync());
        TestAssert.Equal(0, await context.AssetLogs.CountAsync());
    }

    private static async Task AssertDevelopmentSeedAsync(SqliteTestDatabase database)
    {
        await using var context = database.CreateContext();

        TestAssert.Equal(7, await context.Users.CountAsync());
        TestAssert.Equal(1, await context.Members.CountAsync());
        TestAssert.Equal(6, await context.Transactions.CountAsync());
        TestAssert.Equal(6, await context.Payments.CountAsync());
        TestAssert.Equal(4, await context.PaymentMethodOptions.CountAsync());
        TestAssert.Equal(6, await context.Roles.CountAsync());
        TestAssert.True(await context.Users.AllAsync(user => user.RoleId != null));
        TestAssert.True(await context.Payments.AnyAsync(payment => payment.ProofUrl != string.Empty));
        TestAssert.True(await context.Transactions.AnyAsync(transaction => transaction.OrderStatus == OrderStatus.Processing));
        TestAssert.True(await context.Transactions.AnyAsync(transaction => transaction.OrderStatus == OrderStatus.Ready));
        TestAssert.True(await context.Transactions.AnyAsync(transaction => transaction.OrderStatus == OrderStatus.Completed));
        TestAssert.True(await context.TableSessions.AnyAsync(session => session.Status == TableSessionStatus.Active));
        TestAssert.True(await context.TableSessions.AnyAsync(session => session.Status == TableSessionStatus.Closed));
        TestAssert.True(await context.AssetLogs.AnyAsync(assetLog => assetLog.Status == LogStatus.Reported));
        TestAssert.True(await context.AssetLogs.AnyAsync(assetLog => assetLog.Status == LogStatus.Approved));
        TestAssert.True(await context.Tables.AnyAsync(table => table.Status == TableStatus.Occupied));
        TestAssert.True(await context.Users.AnyAsync(user => user.Role == UserRole.Member && user.Username == "member"));
    }

    private static async Task<SeedCounts> CaptureCountsAsync(ApplicationDbContext context)
    {
        return new SeedCounts(
            await context.Users.CountAsync(),
            await context.Members.CountAsync(),
            await context.Transactions.CountAsync(),
            await context.TransactionDetails.CountAsync(),
            await context.Payments.CountAsync(),
            await context.PaymentMethodOptions.CountAsync(),
            await context.Roles.CountAsync(),
            await context.TableSessions.CountAsync(),
            await context.AssetLogs.CountAsync(),
            await context.Notifications.CountAsync());
    }

    private sealed record SeedCounts(
        int Users,
        int Members,
        int Transactions,
        int TransactionDetails,
        int Payments,
        int PaymentMethods,
        int Roles,
        int TableSessions,
        int AssetLogs,
        int Notifications);
}
