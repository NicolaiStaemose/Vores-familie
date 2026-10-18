using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VoresFamilie.Domain;

namespace VoresFamilie.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Family> Families => Set<Family>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Family>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).ValueGeneratedNever(); // client-generated; otherwise EF treats new children of tracked aggregates as existing
            e.Property(f => f.Name).HasMaxLength(100).IsRequired();
            e.HasMany(f => f.Members).WithOne().OnDelete(DeleteBehavior.Cascade);
            e.HasMany(f => f.Invitations).WithOne().OnDelete(DeleteBehavior.Cascade);
            e.Navigation(f => f.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(f => f.Invitations).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<FamilyMember>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).ValueGeneratedNever();
            e.Property(m => m.UserId).HasMaxLength(450).IsRequired();
            e.HasIndex(m => m.UserId).IsUnique(); // one family per user
            e.Property(m => m.DisplayName).HasMaxLength(100).IsRequired();
            e.Property(m => m.Permissions).HasConversion(PermissionsConverter).HasMaxLength(1000).IsRequired();
        });

        b.Entity<Invitation>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).ValueGeneratedNever();
            e.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
            e.HasIndex(i => i.TokenHash).IsUnique();
            e.Property(i => i.Email).HasMaxLength(256);
        });
    }

    private sealed record PermissionsJson(Dictionary<Area, AccessLevel> Levels, bool CanCheckOwnItems);

    private static readonly ValueConverter<MemberPermissions, string> PermissionsConverter = new(
        p => JsonSerializer.Serialize(new PermissionsJson(p.Levels.ToDictionary(x => x.Key, x => x.Value), p.CanCheckOwnItems), (JsonSerializerOptions?)null),
        s => ToPermissions(s));

    private static MemberPermissions ToPermissions(string s)
    {
        var j = JsonSerializer.Deserialize<PermissionsJson>(s, (JsonSerializerOptions?)null)!;
        return new MemberPermissions(j.Levels, j.CanCheckOwnItems);
    }
}

