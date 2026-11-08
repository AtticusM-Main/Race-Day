using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests.Infrastructure;

/// <summary>
/// Starts the real API in memory (same routing, JWT authentication and role checks)
/// but swaps SQL Server for an EF Core in-memory database, so tests need no database server.
/// </summary>
public class RaceDayApiFactory : WebApplicationFactory<Program>
{
    public FakeImageStorage Images { get; } = new();

    private readonly string _databaseName = $"RaceDayTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs skips the SQL Server registration in the "Testing" environment.
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<RaceDayDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.RemoveAll<IImageStorage>();
            services.AddSingleton<IImageStorage>(Images);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Creates the schema and loads the seed data (roles, sample users, events...).
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Database.EnsureCreated();

        return host;
    }

    /// <summary>Runs a query directly against the test database, to check what the API saved.</summary>
    public async Task<T> QueryDbAsync<T>(Func<RaceDayDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        return await query(db);
    }
}
