using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VoresFamilie.Application;

namespace VoresFamilie.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Database:Provider = Sqlite (default, local/tests) or SqlServer (Azure SQL). Connection string: ConnectionStrings:Default.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Database:Provider"] ?? "Sqlite";
        var cs = config.GetConnectionString("Default");

        services.AddDbContext<AppDbContext>(o =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                o.UseSqlServer(cs ?? throw new InvalidOperationException("ConnectionStrings:Default mangler."),
                    s => { s.EnableRetryOnFailure(); s.CommandTimeout(90); });
            else
                o.UseSqlite(cs ?? "Data Source=vores-familie.db");
        });

        services.AddScoped<IFamilyRepository, FamilyRepository>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<FamilyService>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.IsSqlServer()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
    }
}
