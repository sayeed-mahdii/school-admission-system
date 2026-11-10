using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AdmissionSystem.Api.Data;

/// <summary>
/// Used only by <c>dotnet ef</c> (migrations add/script). It never connects, so the
/// placeholder connection string does not need to point at a running database.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=admission_system_design;Username=design;Password=design")
            .Options;

        return new AppDbContext(options);
    }
}

