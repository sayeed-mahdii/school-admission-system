using AdmissionSystem.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AdmissionSystem.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ClassEntity> Classes => Set<ClassEntity>();

    public DbSet<Hold> Holds => Set<Hold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClassEntity>(entity =>
        {
            entity.ToTable("Classes", t =>
                t.HasCheckConstraint("CK_Classes_TotalSeats_Positive", "\"TotalSeats\" >= 1"));

            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedNever();

            // Class names are deliberately NOT unique: the same name may be created repeatedly.
            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(ClassEntity.NameMaxLength);

            entity.Property(c => c.TotalSeats).IsRequired();
            entity.Property(c => c.CreatedAt).IsRequired();

            // Stable ordering for GET /classes.
            entity.HasIndex(c => new { c.CreatedAt, c.Id });
        });

        modelBuilder.Entity<Hold>(entity =>
        {
            var allowedStatuses = string.Join(", ", HoldStatus.All.Select(s => $"'{s}'"));

            entity.ToTable("Holds", t =>
                t.HasCheckConstraint("CK_Holds_Status_Valid", $"\"Status\" IN ({allowedStatuses})"));

            entity.HasKey(h => h.Id);
            entity.Property(h => h.Id).ValueGeneratedNever();

            entity.Property(h => h.StudentId)
                .IsRequired()
                .HasMaxLength(Hold.StudentIdMaxLength);

            // Status is persisted as a plain varchar (one of the HoldStatus constants),
            // guarded by the CK_Holds_Status_Valid check constraint above.
            entity.Property(h => h.Status)
                .IsRequired()
                .HasMaxLength(Hold.StatusMaxLength);

            entity.Property(h => h.CreatedAt).IsRequired();
            entity.Property(h => h.ExpiresAt).IsRequired();

            entity.HasOne(h => h.Class)
                .WithMany(c => c.Holds)
                .HasForeignKey(h => h.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            // Derived seat counts: WHERE ClassId = @id AND Status = ... AND ExpiresAt > now.
            entity.HasIndex(h => new { h.ClassId, h.Status, h.ExpiresAt })
                .HasDatabaseName("IX_Holds_ClassId_Status_ExpiresAt");

            // GET /me/holds: WHERE StudentId = @id ORDER BY CreatedAt DESC.
            entity.HasIndex(h => new { h.StudentId, h.CreatedAt })
                .HasDatabaseName("IX_Holds_StudentId_CreatedAt");

            // Safety net: at most one HELD or CONFIRMED hold per student per class.
            // The filter must use the quoted PascalCase column name.
            entity.HasIndex(h => new { h.ClassId, h.StudentId })
                .IsUnique()
                .HasFilter($"\"Status\" IN ('{HoldStatus.Held}', '{HoldStatus.Confirmed}')")
                .HasDatabaseName("UX_Holds_ClassId_StudentId_Active");
        });
    }
}
