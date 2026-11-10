using Microsoft.EntityFrameworkCore;

namespace AdmissionSystem.Api.Data;

public static class DatabaseMigrator
{
    /// <summary>
    /// Applies pending EF Core migrations, retrying while the database is still starting.
    /// A plain loop is used on purpose instead of <c>EnableRetryOnFailure()</c>, which would
    /// force every manual <c>BeginTransactionAsync()</c> block through an execution strategy.
    /// </summary>
    public static async Task MigrateWithRetryAsync(
        IServiceProvider services,
        int maxAttempts = 10,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        var wait = delay ?? TimeSpan.FromSeconds(3);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigrator));

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations applied (attempt {Attempt}).", attempt);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex,
                    "Database not ready (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}s.",
                    attempt, maxAttempts, wait.TotalSeconds);
                await Task.Delay(wait, cancellationToken);
            }
        }
    }
}

