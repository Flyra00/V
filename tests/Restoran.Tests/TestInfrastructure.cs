using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Restoran.Features.Auth.Dtos;
using Restoran.Features.Auth.Services;
using Restoran.Data;
using Restoran.Features.Payments.Services;
using Restoran.Infrastructure.Security;
using Restoran.Models;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Options;
using Restoran.Shared.Results;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Restoran.Tests;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private SqliteTestDatabase(SqliteConnection connection, DbContextOptions<ApplicationDbContext> options)
    {
        Connection = connection;
        Options = options;
    }

    public SqliteConnection Connection { get; }
    public DbContextOptions<ApplicationDbContext> Options { get; }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new SqliteTestDatabase(connection, options);
    }

    public ApplicationDbContext CreateContext() => new(Options);

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
    }
}

internal sealed class FixedDateTimeProvider : IDateTimeProvider
{
    public FixedDateTimeProvider(DateTime now)
    {
        Now = now;
    }

    public DateTime Now { get; }
}

internal sealed class StubTransactionNumberGenerator : ITransactionNumberGenerator
{
    private readonly string _value;

    public StubTransactionNumberGenerator(string value)
    {
        _value = value;
    }

    public Task<string> GenerateAsync(CancellationToken cancellationToken = default) => Task.FromResult(_value);
}

internal sealed class StubChargeConfigurationProvider : IChargeConfigurationProvider
{
    private readonly ChargeConfiguration _configuration;

    public StubChargeConfigurationProvider(
        decimal taxRate = 10m,
        decimal serviceChargeRate = 0m,
        bool isTaxActive = true,
        bool isServiceChargeActive = true,
        string taxName = "PPN",
        string serviceChargeName = "Service Charge")
    {
        _configuration = new ChargeConfiguration
        {
            TaxName = taxName,
            TaxRate = taxRate,
            IsTaxActive = isTaxActive,
            ServiceChargeName = serviceChargeName,
            ServiceChargeRate = serviceChargeRate,
            IsServiceChargeActive = isServiceChargeActive
        };
    }

    public Task<ChargeConfiguration> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_configuration);
}

internal sealed class StubPaymentProofStorage : IPaymentProofStorage
{
    private readonly string _path;

    public StubPaymentProofStorage(string path = "/uploads/payments/test-proof.png")
    {
        _path = path;
    }

    public Task<string> SaveAsync(string transactionNumber, Microsoft.AspNetCore.Http.IFormFile file, CancellationToken cancellationToken = default)
        => Task.FromResult(_path);
}

internal static class TestPaymentData
{
    public static async Task SeedDefaultPaymentMethodsAsync(ApplicationDbContext context)
    {
        if (await context.PaymentMethodOptions.AnyAsync())
        {
            return;
        }

        context.PaymentMethodOptions.AddRange(
            new PaymentMethodOption
            {
                Code = "tunai",
                DisplayName = "Tunai",
                LegacyMethod = PaymentMethod.Tunai,
                IsActive = true,
                IsCustomerFacing = true,
                IsCashierFacing = true,
                SortOrder = 1
            },
            new PaymentMethodOption
            {
                Code = "qris",
                DisplayName = "QRIS",
                LegacyMethod = PaymentMethod.QRIS,
                IsActive = true,
                IsCustomerFacing = true,
                IsCashierFacing = true,
                SortOrder = 2
            },
            new PaymentMethodOption
            {
                Code = "transfer",
                DisplayName = "Transfer",
                LegacyMethod = PaymentMethod.Transfer,
                IsActive = true,
                IsCustomerFacing = true,
                IsCashierFacing = true,
                SortOrder = 3
            },
            new PaymentMethodOption
            {
                Code = "bayar-di-kasir",
                DisplayName = "Bayar di Kasir",
                LegacyMethod = PaymentMethod.BayarDiKasir,
                IsActive = true,
                IsCustomerFacing = true,
                IsCashierFacing = false,
                SortOrder = 4
            });

        await context.SaveChangesAsync();
    }

    public static IPaymentService CreatePaymentService(ApplicationDbContext context) => new PaymentService(context);

    public static async Task<Payment> SeedPaymentAsync(
        ApplicationDbContext context,
        int transactionId,
        PaymentMethod method,
        PaymentStatus status,
        decimal amount,
        DateTime? paymentDate = null,
        string proofUrl = "")
    {
        await SeedDefaultPaymentMethodsAsync(context);

        var methodOption = await context.PaymentMethodOptions.SingleAsync(option => option.LegacyMethod == method);
        var payment = new Payment
        {
            TransactionId = transactionId,
            PaymentMethodOptionId = methodOption.Id,
            Amount = amount,
            PaymentStatus = status,
            PaymentDate = paymentDate,
            ProofUrl = proofUrl
        };

        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        return payment;
    }
}

internal sealed class StubMidtransService : IMidtransService
{
    private readonly Func<Transaction, CancellationToken, Task<MidtransSnapResponse>> _createSnap;
    private readonly Func<string, CancellationToken, Task<MidtransStatusResponse>> _getStatus;
    private readonly Func<int, CancellationToken, Task<OperationResult>> _applyStatus;

    public StubMidtransService(
        Func<Transaction, CancellationToken, Task<MidtransSnapResponse>>? createSnap = null,
        Func<string, CancellationToken, Task<MidtransStatusResponse>>? getStatus = null,
        Func<int, CancellationToken, Task<OperationResult>>? applyStatus = null)
    {
        _createSnap = createSnap ?? ((_, _) => Task.FromResult(MidtransSnapResponse.Success("snap-token-test", "https://app.sandbox.midtrans.com/snap/v2/vtweb/test", "{}")));
        _getStatus = getStatus ?? ((_, _) => Task.FromResult(MidtransStatusResponse.NotFound("Transaksi Midtrans belum dipilih/dibayar oleh pelanggan atau belum tersedia di Midtrans.")));
        _applyStatus = applyStatus ?? ((_, _) => Task.FromResult(OperationResult.Failure("Transaksi Midtrans belum dipilih/dibayar oleh pelanggan atau belum tersedia di Midtrans.")));
    }

    public Task<MidtransSnapResponse> CreateSnapTransactionAsync(Transaction transaction, CancellationToken ct = default)
        => _createSnap(transaction, ct);

    public Task<MidtransStatusResponse> GetTransactionStatusAsync(string midtransOrderId, CancellationToken ct = default)
        => _getStatus(midtransOrderId, ct);

    public Task<OperationResult> ApplyStatusToPaymentAsync(int transactionId, CancellationToken ct = default)
        => _applyStatus(transactionId, ct);

    public Task<OperationResult<MidtransPaymentUpdateResult>> ProcessNotificationAsync(MidtransNotificationRequest notification, CancellationToken ct = default)
        => Task.FromResult(OperationResult<MidtransPaymentUpdateResult>.Failure("Not implemented in stub."));
}

internal sealed class DelegatingHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public DelegatingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => _handler(request, cancellationToken);
}

internal static class MidtransTestFactory
{
    public static MidtransService CreateService(
        ApplicationDbContext context,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        string serverKey = "SB-Mid-server-test")
    {
        var httpClient = new HttpClient(new DelegatingHttpMessageHandler(handler));
        var options = Options.Create(new MidtransOptions
        {
            ServerKey = serverKey,
            ClientKey = "SB-Mid-client-test"
        });

        return new MidtransService(httpClient, context, options, NullLogger<MidtransService>.Instance);
    }

    public static string CreateNotificationSignature(string orderId, string statusCode, string grossAmount, string serverKey)
    {
        var source = string.Concat(orderId, statusCode, grossAmount, serverKey);
        var hash = SHA512.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

internal static class TestRoleData
{
    public static async Task SeedDefaultRolesAsync(ApplicationDbContext context)
    {
        if (await context.Roles.AnyAsync())
        {
            return;
        }

        context.Roles.AddRange(
            CreateRole(1, "Administrator", UserRole.Admin, 1),
            CreateRole(2, "Owner", UserRole.Owner, 2),
            CreateRole(3, "Supervisor", UserRole.Supervisor, 3),
            CreateRole(4, "Kasir", UserRole.Kasir, 4),
            CreateRole(5, "Bagian Masak", UserRole.BagianMasak, 5),
            CreateRole(6, "Member", UserRole.Member, 6));

        await context.SaveChangesAsync();
    }

    public static async Task<int> GetRoleIdAsync(ApplicationDbContext context, UserRole role)
    {
        var roleEntity = await context.Roles.SingleAsync(entity => entity.Code == RoleBridge.GetSystemRoleCode(role));
        return roleEntity.Id;
    }

    private static Role CreateRole(int id, string name, UserRole role, int sortOrder)
    {
        return new Role
        {
            Id = id,
            Name = name,
            Code = RoleBridge.GetSystemRoleCode(role),
            IsSystemRole = true,
            IsActive = true,
            SortOrder = sortOrder
        };
    }
}

internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "Restoran.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal static class TestOptions
{
    public static IOptions<AppSettingsOptions> CreateAppSettings(decimal taxRate = 0.10m)
        => Options.Create(new AppSettingsOptions
        {
            CompanyName = "Test Restoran",
            TaxRate = taxRate,
            ServiceChargeRate = 0.05m,
            PointsPerTransaction = 10
        });
}

internal sealed class StubAuthCookieService : IAuthCookieService
{
    public Task SignInAsync(HttpContext httpContext, AuthenticatedSession session) => Task.CompletedTask;
    public Task SignOutAsync(HttpContext httpContext) => Task.CompletedTask;
    public int? GetUserId(HttpRequest request) => null;
    public AuthenticatedSession? GetAuthenticatedSession(HttpRequest request) => null;
    public UserRole? GetUserRole(HttpRequest request) => null;
}

internal static class TestAssert
{
    public static void True(bool condition, string message = "Expected condition to be true.")
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message = "Expected condition to be false.")
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(message ?? $"Expected '{expected}', got '{actual}'.");
        }
    }

    public static void NotNull(object? value, string message = "Expected value to be non-null.")
    {
        if (value is null)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Null(object? value, string message = "Expected value to be null.")
    {
        if (value is not null)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void All<T>(IEnumerable<T> values, Action<T> assertion)
    {
        foreach (var value in values)
        {
            assertion(value);
        }
    }
}
