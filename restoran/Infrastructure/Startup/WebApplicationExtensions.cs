using Microsoft.EntityFrameworkCore;
using Restoran.Data;
using Microsoft.Data.Sqlite;

namespace Restoran.Infrastructure.Startup
{
    public static class WebApplicationExtensions
    {
        public static async Task InitializeDatabaseAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                var hasMigrations = context.Database.GetMigrations().Any();
                if (hasMigrations)
                {
                    if (await ShouldUseMigrationPathAsync(context))
                    {
                        await context.Database.MigrateAsync();
                    }
                    else
                    {
                        await context.Database.EnsureCreatedAsync();
                    }
                }
                else
                {
                    await context.Database.EnsureCreatedAsync();
                }

                await SeedData.Initialize(services, app.Environment);
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred while seeding the database.");
            }
        }

        private static async Task<bool> ShouldUseMigrationPathAsync(ApplicationDbContext context)
        {
            if (!await context.Database.CanConnectAsync())
            {
                return true;
            }

            await using var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name NOT LIKE 'sqlite_%'
            """;

            var tableCount = Convert.ToInt32(await command.ExecuteScalarAsync());
            if (tableCount == 0)
            {
                return true;
            }

            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name = '__EFMigrationsHistory'
            """;

            var hasHistoryTable = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
            return hasHistoryTable;
        }
    }
}
