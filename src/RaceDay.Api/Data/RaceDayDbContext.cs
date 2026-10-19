using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core (code-first) context. The table/column configuration mirrors docs/RaceDaySchema.sql.
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");
            entity.HasKey(r => r.RoleId);
            entity.Property(r => r.RoleName).HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.HasIndex(r => r.RoleName).IsUnique().HasDatabaseName("UQ_Role_RoleName");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("User");
            entity.HasKey(u => u.UserId);
            entity.Property(u => u.FullName).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(150).IsUnicode(false).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(255).IsUnicode(false).IsRequired();
            entity.Property(u => u.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            entity.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_User_Email");

            entity.HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .HasConstraintName("FK_User_Role")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Event", t =>
                t.HasCheckConstraint("CK_Event_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')"));
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.Name).HasMaxLength(150).IsUnicode(false).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500).IsUnicode(false);
            entity.Property(e => e.EventDate).HasColumnType("datetime");
            entity.Property(e => e.Location).HasMaxLength(150).IsUnicode(false).IsRequired();
            entity.Property(e => e.Distance).HasColumnType("decimal(5,2)");
            entity.Property(e => e.EventType).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");

            entity.HasOne(e => e.Organiser)
                .WithMany(u => u.OrganisedEvents)
                .HasForeignKey(e => e.OrganiserId)
                .HasConstraintName("FK_Event_Organiser")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Category");
            entity.HasKey(c => c.CategoryId);
            entity.Property(c => c.Name).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(c => c.Distance).HasColumnType("decimal(5,2)");

            entity.HasOne(c => c.Event)
                .WithMany(e => e.Categories)
                .HasForeignKey(c => c.EventId)
                .HasConstraintName("FK_Category_Event")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Enrolment>(entity =>
        {
            entity.ToTable("Enrolment");
            entity.HasKey(e => e.EnrolmentId);
            entity.Property(e => e.EnrolmentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");

            entity.HasOne(e => e.Participant)
                .WithMany(u => u.Enrolments)
                .HasForeignKey(e => e.ParticipantId)
                .HasConstraintName("FK_Enrolment_Participant")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Enrolments)
                .HasForeignKey(e => e.CategoryId)
                .HasConstraintName("FK_Enrolment_Category")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Result>(entity =>
        {
            entity.ToTable("Result");
            entity.HasKey(r => r.ResultId);
            entity.Property(r => r.FinishTime).HasColumnType("time");
            entity.Property(r => r.RecordedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");

            entity.HasIndex(r => r.EnrolmentId).IsUnique();
            entity.HasOne(r => r.Enrolment)
                .WithOne(e => e.Result)
                .HasForeignKey<Result>(r => r.EnrolmentId)
                .HasConstraintName("FK_Result_Enrolment")
                .OnDelete(DeleteBehavior.Cascade);
        });

        SeedData(modelBuilder);
    }

    /// <summary>
    /// Same sample data as the Part 1 SQL script. All seeded users have the password "Password123!".
    /// </summary>
    private static void SeedData(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTime(2026, 9, 1, 8, 0, 0);

        modelBuilder.Entity<Role>().HasData(
            new Role { RoleId = 1, RoleName = Role.Organiser },
            new Role { RoleId = 2, RoleName = Role.Participant });

        modelBuilder.Entity<User>().HasData(
            new User { UserId = 1, RoleId = 1, FullName = "Naledi Khumalo", Email = "naledi.khumalo@raceday.co.za", CreatedAt = seedDate,
                PasswordHash = "AQAAAAIAAYagAAAAEDk+C1J4uROV2oSQmmBgDXzS+KwwS1h7rw0GMemaoQuaC9GsEYpFwcxFy9MFc7WF6Q==" },
            new User { UserId = 2, RoleId = 1, FullName = "Johan Botha", Email = "johan.botha@raceday.co.za", CreatedAt = seedDate,
                PasswordHash = "AQAAAAIAAYagAAAAEKsDkXCS6dxTig6+ORCDmGYbgYcXkdhq20qmul9Ec/bqZGaUTC7SKoXW4J/HFtd9AQ==" },
            new User { UserId = 3, RoleId = 2, FullName = "Thandiwe Nkosi", Email = "thandiwe.nkosi@example.com", CreatedAt = seedDate,
                PasswordHash = "AQAAAAIAAYagAAAAEFJWFVSDlBvwSX8dWVV+BOdNGP+yvWuWxVktQdOLPIvy3ZqtHaJPdUjLF5fS1s+pYw==" },
            new User { UserId = 4, RoleId = 2, FullName = "Ryan Pillay", Email = "ryan.pillay@example.com", CreatedAt = seedDate,
                PasswordHash = "AQAAAAIAAYagAAAAEN+iUFzbRO5j8njgO0zx1VXqSRMjdpVeCA5/W0SCnBLQq+hEAeumNHfVZ1vv4AQwVw==" });

        modelBuilder.Entity<Event>().HasData(
            new Event { EventId = 1, OrganiserId = 1, Name = "Cape Town Spring Run", Description = "Annual road running event through the city bowl.",
                EventDate = new DateTime(2026, 10, 18, 7, 0, 0), Location = "Cape Town, South Africa", Distance = 10.00m, EventType = EventType.Run, CreatedAt = seedDate },
            new Event { EventId = 2, OrganiserId = 1, Name = "Durban Beachfront Race", Description = "Coastal race along the Golden Mile.",
                EventDate = new DateTime(2026, 11, 8, 6, 30, 0), Location = "Durban, South Africa", Distance = 21.10m, EventType = EventType.Run, CreatedAt = seedDate },
            new Event { EventId = 3, OrganiserId = 2, Name = "Joburg Trail Challenge", Description = "Off-road trail run through the Northern suburbs.",
                EventDate = new DateTime(2026, 11, 22, 6, 0, 0), Location = "Johannesburg, South Africa", Distance = 15.00m, EventType = EventType.Run, CreatedAt = seedDate });

        modelBuilder.Entity<Category>().HasData(
            new Category { CategoryId = 1, EventId = 1, Name = "5km", Distance = 5.00m, MaxParticipants = 500 },
            new Category { CategoryId = 2, EventId = 1, Name = "10km", Distance = 10.00m, MaxParticipants = 300 },
            new Category { CategoryId = 3, EventId = 2, Name = "5km", Distance = 5.00m, MaxParticipants = 400 },
            new Category { CategoryId = 4, EventId = 2, Name = "21km", Distance = 21.10m, MaxParticipants = 150 },
            new Category { CategoryId = 5, EventId = 3, Name = "15km", Distance = 15.00m, MaxParticipants = 200 });

        modelBuilder.Entity<Enrolment>().HasData(
            new Enrolment { EnrolmentId = 1, ParticipantId = 3, CategoryId = 2, EnrolmentDate = seedDate },
            new Enrolment { EnrolmentId = 2, ParticipantId = 4, CategoryId = 2, EnrolmentDate = seedDate },
            new Enrolment { EnrolmentId = 3, ParticipantId = 3, CategoryId = 4, EnrolmentDate = seedDate });

        modelBuilder.Entity<Result>().HasData(
            new Result { ResultId = 1, EnrolmentId = 1, FinishTime = new TimeSpan(0, 52, 14), Position = 3, RecordedAt = seedDate });
    }
}
