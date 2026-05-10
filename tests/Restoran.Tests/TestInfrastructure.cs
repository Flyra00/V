using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Restoran.Data;
using Restoran.Shared.Abstractions;
using Restoran.Shared.Options;

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
