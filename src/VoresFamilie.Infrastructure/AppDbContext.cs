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
    public DbSet<FamilyCalendar> Calendars => Set<FamilyCalendar>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<HomeworkCompletion> HomeworkCompletions => Set<HomeworkCompletion>();
    public DbSet<GoogleConnection> GoogleConnections => Set<GoogleConnection>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<ChecklistCheck> ChecklistChecks => Set<ChecklistCheck>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

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

        b.Entity<FamilyCalendar>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.GoogleCalendarId).HasMaxLength(300).IsRequired();
            e.Property(c => c.ConnectedByUserId).HasMaxLength(450).IsRequired();
            e.Property(c => c.Color).HasMaxLength(20).IsRequired();
            e.Property(c => c.SyncToken).HasMaxLength(500);
            e.Property(c => c.LastSyncedAt).HasConversion(UtcTicksNullable);
            e.HasIndex(c => new { c.FamilyId, c.GoogleCalendarId }).IsUnique();
        });

        b.Entity<CalendarEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.GoogleEventId).HasMaxLength(300).IsRequired();
            e.Property(x => x.ETag).HasMaxLength(100);
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Location).HasMaxLength(500);
            // Stored as UTC ticks so ranges can be compared and sorted on SQLite and SQL Server alike.
            e.Property(x => x.Start).HasConversion(UtcTicks);
            e.Property(x => x.End).HasConversion(UtcTicks);
            e.Property(x => x.UpdatedAt).HasConversion(UtcTicksNullable);
            e.HasIndex(x => new { x.CalendarId, x.GoogleEventId }).IsUnique();
            e.HasIndex(x => new { x.CalendarId, x.Start });
        });

        b.Entity<ChecklistItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).ValueGeneratedNever();
            e.Property(i => i.Title).HasMaxLength(200).IsRequired();
            e.Property(i => i.Days).HasConversion<int>();
            e.Property(i => i.Icon).HasMaxLength(ChecklistIcons.MaxLength);
            e.HasIndex(i => new { i.FamilyId, i.Kind, i.MemberId });
        });

        b.Entity<ChecklistCheck>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.CompletedAt).HasConversion(UtcTicks);
            e.HasIndex(c => new { c.ItemId, c.Date }).IsUnique();
        });

        b.Entity<TodoItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.Title).HasMaxLength(300).IsRequired();
            e.Property(t => t.Notes).HasMaxLength(2000);
            e.Property(t => t.CreatedAt).HasConversion(UtcTicks);
            e.Property(t => t.CompletedAt).HasConversion(UtcTicksNullable);
            e.HasIndex(t => t.FamilyId);
        });

        b.Entity<GoogleConnection>(e =>
        {
            e.HasKey(g => g.UserId);
            e.Property(g => g.UserId).HasMaxLength(450);
            e.Property(g => g.EncryptedRefreshToken).HasMaxLength(2000).IsRequired();
            e.Property(g => g.ConnectedAt).HasConversion(UtcTicks);
        });

        b.Entity<HomeworkCompletion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.GoogleEventId).HasMaxLength(300).IsRequired();
            e.Property(x => x.CompletedAt).HasConversion(UtcTicks);
            e.HasIndex(x => new { x.CalendarId, x.GoogleEventId }).IsUnique();
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

    private static readonly ValueConverter<DateTimeOffset, long> UtcTicks = new(
        v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> UtcTicksNullable = new(
        v => v == null ? null : v.Value.UtcTicks, v => v == null ? null : new DateTimeOffset(v.Value, TimeSpan.Zero));

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





