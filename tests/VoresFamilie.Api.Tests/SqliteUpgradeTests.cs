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

    [Fact]
    public async Task Missing_columns_are_added_to_an_existing_table()
    {
        _ = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Simulate a database from before the Icon column existed.
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE ChecklistItems DROP COLUMN Icon");
            Assert.False(await HasColumn(db, "ChecklistItems", "Icon"));
        }

        await factory.Services.InitializeDatabaseAsync();

        using var after = factory.Services.CreateScope();
        var db2 = after.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await HasColumn(db2, "ChecklistItems", "Icon"));
        Assert.Empty(await db2.ChecklistItems.ToListAsync()); // the table is usable with the restored column
    }

    private static async Task<bool> HasColumn(AppDbContext db, string table, string column) =>
        await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info({table}) WHERE name = {column}").SingleAsync() > 0;

    private static async Task<bool> HasTable(AppDbContext db, string name) =>
        await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE name = {name}").SingleAsync() > 0;
}

