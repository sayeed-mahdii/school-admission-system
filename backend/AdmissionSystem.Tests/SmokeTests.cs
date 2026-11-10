using System.Net;
using AdmissionSystem.Api.Data;
using AdmissionSystem.Api.Domain;
using AdmissionSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdmissionSystem.Tests;

public sealed class SmokeTests(AdmissionApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Database_MigrationsAreApplied_AndConnectivityWorks()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(await db.Database.CanConnectAsync());

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
        Assert.Empty(pendingMigrations);

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(appliedMigrations);

        Assert.Equal(0, await db.Classes.CountAsync());
        Assert.Equal(0, await db.Holds.CountAsync());

        // Seed a class and advance clock to verify the next test starts from a clean slate.
        db.Classes.Add(new ClassEntity
        {
            Id = Guid.NewGuid(),
            Name = "Smoke Test Class",
            TotalSeats = 10,
            CreatedAt = Clock.GetUtcNow(),
        });
        await db.SaveChangesAsync();
        Clock.Advance(TimeSpan.FromMinutes(15));

        Assert.Equal(1, await db.Classes.CountAsync());
    }

    [Fact]
    public async Task PerTestReset_CleansDatabase_AndResetsFakeClock()
    {
        Assert.Equal(AdmissionApiFactory.InitialUtcNow, Clock.GetUtcNow());

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(0, await db.Classes.CountAsync());
        Assert.Equal(0, await db.Holds.CountAsync());

        Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(AdmissionApiFactory.InitialUtcNow.AddMinutes(5), Clock.GetUtcNow());

        var response = await Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
