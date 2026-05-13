using DocumEntum.Data;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DocumEntum.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Новые DbSet для СЭД
    public DbSet<Department> Departments { get; set; }
    public DbSet<Position> Positions { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<EmployeePosition> EmployeePositions { get; set; }
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<WorkflowState> WorkflowStates { get; set; }
    public DbSet<WorkflowTransition> WorkflowTransitions { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<DocumentVersion> DocumentVersions { get; set; }
    public DbSet<DocumentHistory> DocumentHistories { get; set; }
    public DbSet<DocumentType> DocumentTypes { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Email и NormalizedEmail nullable
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.Email).IsRequired(false);
            entity.Property(e => e.NormalizedEmail).IsRequired(false);
        });

        builder.Entity<Department>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).IsRequired().HasMaxLength(200);

            entity.HasOne(d => d.Parent)
                .WithMany(d => d.Children)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Индекс для быстрого поиска по Path (ltree)
            entity.HasIndex(d => d.Path).HasMethod("gist");
        });

        builder.Entity<Position>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Title).IsRequired().HasMaxLength(100);

            //должность уникальна внутри отдела
            entity.HasIndex(p => new { p.DepartmentId, p.Title }).IsUnique();

            //Department
            entity.HasOne(p => p.Department)
                .WithMany(d => d.Positions)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            //EmployeePosition
            entity.HasMany(p => p.EmployeePositions)
        .WithOne(ep => ep.Position)
        .HasForeignKey(ep => ep.PositionId)
        .OnDelete(DeleteBehavior.Restrict);

        });

        builder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(256);

            // Связь с IdentityUser
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeePosition>(entity =>
        {
            entity.HasKey(ep => ep.Id);

            entity.HasOne(ep => ep.Employee)
                .WithMany()
                .HasForeignKey(ep => ep.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);


            entity.HasOne(ep => ep.Position)
         .WithMany(p => p.EmployeePositions)
         .HasForeignKey(ep => ep.PositionId)
         .OnDelete(DeleteBehavior.Restrict);

            //сотрудник не занимает одну и ту же должность
            entity.HasIndex(ep => new { ep.EmployeeId, ep.PositionId }).IsUnique();

        });

        builder.Entity<Workflow>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Name).IsRequired().HasMaxLength(200);

            entity.HasOne(w => w.DocumentType)
                .WithMany(dt => dt.Workflows)
                .HasForeignKey(w => w.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WorkflowState>(entity =>
        {
            entity.HasKey(ws => ws.Id);
            entity.Property(ws => ws.Name).IsRequired().HasMaxLength(100);

            // Связь Workflow -> States
            entity.HasOne(ws => ws.Workflow)
                .WithMany(w => w.States)
                .HasForeignKey(ws => ws.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ws => ws.RequiredPosition)
                .WithMany()
                .HasForeignKey(ws => ws.RequiredPositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WorkflowTransition>(entity =>
        {
            entity.HasKey(wt => wt.Id);

            entity.HasOne(wt => wt.Workflow)
                .WithMany()
                .HasForeignKey(wt => wt.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(wt => wt.FromState)
                .WithMany()
                .HasForeignKey(wt => wt.FromStateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(wt => wt.ToState)
                .WithMany()
                .HasForeignKey(wt => wt.ToStateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DocumentType>(entity =>
        {
            entity.HasKey(dt => dt.Id);
            entity.Property(dt => dt.Name).IsRequired().HasMaxLength(200);
            entity.HasMany(dt => dt.AvailableDepartments)
                .WithMany()
                .UsingEntity(j => j.ToTable("DocumentTypeDepartments"));
        });

        builder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Title).IsRequired().HasMaxLength(500);

            // Новые файловые поля
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(500);
            entity.Property(d => d.StoredFileName).IsRequired().HasMaxLength(260);
            entity.Property(d => d.FileExtension).IsRequired().HasMaxLength(50);
            entity.Property(d => d.FileSize).IsRequired();
            entity.Property(d => d.ContentType).IsRequired().HasMaxLength(200);

            //компаратор
            var extraAttributesComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, object>>(
                (d1, d2) => d1.Count == d2.Count && !d1.Except(d2).Any(),
                d => d.Aggregate(0, (a, p) => HashCode.Combine(
                a, p.Key.GetHashCode(), p.Value != null ? p.Value.GetHashCode() : 0)),
                d => d.ToDictionary(k => k.Key, k => k.Value)
            );
            // JSONB для динамических атрибутов через конвертер значений
            entity.Property(d => d.ExtraAttributes)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions)null)
                 ?? new Dictionary<string, object>()
            ).Metadata.SetValueComparer(extraAttributesComparer);

            entity.HasOne(d => d.Author)
                .WithMany()
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.CurrentState)
                .WithMany()
                .HasForeignKey(d => d.CurrentStateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Workflow)
                .WithMany()
                .HasForeignKey(d => d.WorkflowId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.DocumentType)
                .WithMany()
                .HasForeignKey(d => d.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Department)
                .WithMany()
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.ReplacedDocument)
                .WithMany()
                .HasForeignKey(d => d.ReplacesDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Индексы для поиска
            entity.HasIndex(d => d.CreatedAt);
            entity.HasIndex(d => d.CurrentStateId);
            entity.HasIndex(d => d.DepartmentId);
            entity.HasIndex(d => d.ReplacesDocumentId);
        });

        builder.Entity<DocumentVersion>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Title).IsRequired().HasMaxLength(500);
            entity.Property(v => v.FileName).IsRequired().HasMaxLength(500);
            entity.Property(v => v.StoredFileName).IsRequired().HasMaxLength(260);
            entity.Property(v => v.FileExtension).IsRequired().HasMaxLength(50);
            entity.Property(v => v.ContentType).IsRequired().HasMaxLength(200);

            var versionExtraComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, object>>(
                (d1, d2) => d1.Count == d2.Count && !d1.Except(d2).Any(),
                d => d.Aggregate(0, (a, p) => HashCode.Combine(
                    a, p.Key.GetHashCode(), p.Value != null ? p.Value.GetHashCode() : 0)),
                d => d.ToDictionary(k => k.Key, k => k.Value)
            );
            entity.Property(v => v.ExtraAttributes)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions)null)
                        ?? new Dictionary<string, object>()
                ).Metadata.SetValueComparer(versionExtraComparer);

            entity.HasOne(v => v.Document)
                .WithMany()
                .HasForeignKey(v => v.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(v => new { v.DocumentId, v.VersionNumber });
        });

        builder.Entity<DocumentHistory>(entity =>
        {
            entity.HasKey(h => h.Id);

            entity.HasOne(h => h.Document)
                .WithMany()
                .HasForeignKey(h => h.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.ActionBy)
                .WithMany()
                .HasForeignKey(h => h.ActionById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(h => h.ActionAt);
        });

        // для иерархических путей отделов
        builder.HasPostgresExtension("ltree");

        // конвертация DateTime в UTC
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                        v => v.ToUniversalTime(),
                        v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
                }
            }
        }
    }
}