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
        services.AddScoped<ICalendarRepository, CalendarRepository>();
        services.AddScoped<CalendarSyncService>();
        services.AddScoped<CalendarService>();
        services.AddScoped<HomeworkService>();
        services.AddScoped<IChecklistRepository, ChecklistRepository>();
        services.AddScoped<ITodoRepository, TodoRepository>();
        services.AddScoped<ChecklistService>();
        services.AddScoped<TodoService>();

        // Google:Mode=Fake runs the calendar features against an in-memory Google (no Google Cloud project needed).
        if (string.Equals(config["Google:Mode"], "Fake", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<FakeGoogleCalendarGateway>(sp =>
            {
                var fake = new FakeGoogleCalendarGateway(sp.GetRequiredService<IClock>());
                fake.SeedDemo();
                return fake;
            });
            services.AddSingleton<IGoogleCalendarGateway>(sp => sp.GetRequiredService<FakeGoogleCalendarGateway>());
            services.AddSingleton<IGoogleAuthService>(sp => sp.GetRequiredService<FakeGoogleCalendarGateway>());
        }
        else
        {
            services.Configure<GoogleCalendarOptions>(config.GetSection("Google:Calendar"));
            services.AddSingleton<GoogleAuthService>();
            services.AddSingleton<IGoogleAuthService>(sp => sp.GetRequiredService<GoogleAuthService>());
            services.AddSingleton<IGoogleCalendarGateway, GoogleCalendarGateway>();
        }
        services.AddHostedService<CalendarSyncHostedService>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.IsSqlServer()) await db.Database.MigrateAsync();
        else
        {
            await db.Database.EnsureCreatedAsync();
            await AddMissingSqliteObjectsAsync(db);
        }
    }

    /// <summary>
    /// EnsureCreated does nothing when the SQLite file already exists, so a dev database from an earlier phase would miss
    /// new tables. Run the CREATE statements for tables/indexes that are not there yet (SQLite is for local use only;
    /// Azure SQL uses real migrations).
    /// </summary>
    private static async Task AddMissingSqliteObjectsAsync(AppDbContext db)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await db.Database.OpenConnectionAsync();
        try
        {
            await using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) existing.Add(reader.GetString(0));
            }

            foreach (var raw in System.Text.RegularExpressions.Regex.Split(db.Database.GenerateCreateScript(), @";\s*\r?\n"))
            {
                var statement = raw.Trim().TrimEnd(';').Trim();
                var m = System.Text.RegularExpressions.Regex.Match(statement, "^CREATE (?:UNIQUE )?(?:TABLE|INDEX) \"([^\"]+)\"");
                if (!m.Success || existing.Contains(m.Groups[1].Value)) continue;
                await db.Database.ExecuteSqlRawAsync(statement);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}





