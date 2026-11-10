using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace AdmissionSystem.Tests.Infrastructure;

public sealed class AdmissionApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset InitialUtcNow =
        new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private const string DefaultLocalTestConnectionString =
        "Host=localhost;Port=5432;Database=admission_system_test;Username=admin;Password=admin;Maximum Pool Size=30;Timeout=30;GSS Encryption Mode=Disable";

    public FakeTimeProvider Clock { get; private set; } = new(InitialUtcNow);

    public AdmissionApiFactory()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")) &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")))
        {
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                DefaultLocalTestConnectionString);
        }
    }

    public void ResetClock()
    {
        Clock = new FakeTimeProvider(InitialUtcNow);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddTransient<TimeProvider>(_ => Clock);
        });
    }
}

