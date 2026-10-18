using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VoresFamilie.Infrastructure;

/// <summary>Used only by `dotnet ef` to generate SQL Server migrations.</summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer("Server=(local);Database=VoresFamilie;Trusted_Connection=True;TrustServerCertificate=True").Options);
}
