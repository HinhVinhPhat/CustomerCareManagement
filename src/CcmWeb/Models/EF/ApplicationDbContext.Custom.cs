using CcmWeb.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Models.EF;

/// <summary>
/// Everything that the scaffold would overwrite on the next run lives here instead of in
/// <c>ApplicationDbContext.cs</c>. See the ccm-mvc-architecture skill.
/// </summary>
public partial class ApplicationDbContext
{
    /// <summary>
    /// The Vietnamese province/ward catalogue, added here instead of in the scaffolded context so a
    /// later scaffold does not overwrite it. The application only reads these rows.
    /// </summary>
    public virtual DbSet<Province> Provinces => Set<Province>();

    public virtual DbSet<Ward> Wards => Set<Ward>();

    /// <summary>
    /// Connection of the project database exactly as it looks in SQL Server Management Studio:
    /// instance <c>localhost\SQLEXPRESS</c>, Windows Authentication, database <c>CustomerCareDB</c>
    /// (created by <c>database/01</c>-<c>04</c>). The running application reaches the same database
    /// through the <c>ConnectionStrings:DefaultConnection</c> setting (User Secrets) and signs in as
    /// <c>app_ccm</c>; this constant is what EF uses when the context is built without options, for
    /// example from a scratch console, a script or a design-time tool.
    /// </summary>
    public const string FallbackConnectionString =
        @"Server=localhost\SQLEXPRESS;Database=CustomerCareDB;Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>
    /// Parameterless constructor, needed when the context is built without options so that
    /// <see cref="OnConfiguring"/> can apply <see cref="FallbackConnectionString"/>.
    /// </summary>
    public ApplicationDbContext()
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Dependency injection already configured the provider for the web application, so never
        // overwrite it; only help when nothing else did.
        if (optionsBuilder.IsConfigured)
        {
            return;
        }

        optionsBuilder.UseSqlServer(FallbackConnectionString);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // These four tables have triggers. SQL Server rejects OUTPUT (without INTO) on such a
        // table, which is what EF sends by default, so it must be turned off or every
        // SaveChanges fails with error 334.
        modelBuilder.Entity<Contract>().ToTable(tb => tb.UseSqlOutputClause(false));
        modelBuilder.Entity<ContractDetail>().ToTable(tb => tb.UseSqlOutputClause(false));
        modelBuilder.Entity<Customer>().ToTable(tb => tb.UseSqlOutputClause(false));
        modelBuilder.Entity<Feedback>().ToTable(tb => tb.UseSqlOutputClause(false));

        // M06 gave these three tables an audit trigger as well, so the same rule applies to them.
        modelBuilder.Entity<ProductService>().ToTable(tb => tb.UseSqlOutputClause(false));
        modelBuilder.Entity<Employee>().ToTable(tb => tb.UseSqlOutputClause(false));
        modelBuilder.Entity<Account>().ToTable(tb => tb.UseSqlOutputClause(false));

        // Address catalogue and the two optional columns on CUSTOMER. The relationship is
        // configured here because the scaffolded Customer class knows nothing about them.
        modelBuilder.Entity<Province>(entity =>
        {
            entity.HasKey(e => e.ProvinceCode);
            entity.ToTable("PROVINCE");
            entity.Property(e => e.ProvinceCode).ValueGeneratedNever();
            entity.Property(e => e.ProvinceName).HasMaxLength(100);
            entity.Property(e => e.DivisionType).HasMaxLength(50);
        });

        modelBuilder.Entity<Ward>(entity =>
        {
            entity.HasKey(e => e.WardCode);
            entity.ToTable("WARD");
            entity.Property(e => e.WardCode).ValueGeneratedNever();
            entity.Property(e => e.WardName).HasMaxLength(100);
            entity.Property(e => e.DivisionType).HasMaxLength(50);
            entity.HasIndex(e => e.ProvinceCode);
            entity.HasOne<Province>()
                  .WithMany()
                  .HasForeignKey(e => e.ProvinceCode)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasOne<Province>()
                  .WithMany()
                  .HasForeignKey(e => e.ProvinceCode)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Ward>()
                  .WithMany()
                  .HasForeignKey(e => e.WardCode)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
