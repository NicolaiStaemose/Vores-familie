using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VoresFamilie.Infrastructure;

namespace VoresFamilie.Api.Tests;

public class SqliteUpgradeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Missing_tables_are_added_to_an_existing_sqlite_file()
    {
        _ = factory.CreateClient(); // starts the app, which creates the full schema
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Simulate a database from an earlier phase that has no calendar tables.
            await db.Database.ExecuteSqlRawAsync("DROP TABLE CalendarEvents");
            await db.Database.ExecuteSqlRawAsync("DROP TABLE HomeworkCompletions");
            Assert.False(await HasTable(db, "CalendarEvents"));
        }

        await factory.Services.InitializeDatabaseAsync();

        using var after = factory.Services.CreateScope();
        var db2 = after.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await HasTable(db2, "CalendarEvents"));
        Assert.True(await HasTable(db2, "HomeworkCompletions"));
        Assert.Empty(await db2.CalendarEvents.ToListAsync()); // the restored table is usable
    }

    private static async Task<bool> HasTable(AppDbContext db, string name) =>
        await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE name = {name}").SingleAsync() > 0;
}
