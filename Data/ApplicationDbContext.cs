using KarlixID.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KarlixID.Web.Data
{
    // VAŽNO: prelazimo na IdentityDbContext da bi Identity (UserManager/RoleManager) radio ispravno.
    public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Tvoje domenske tablice
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<TblTest> TblTests => Set<TblTest>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // === Tenant ===
            builder.Entity<Tenant>(e =>
            {
                e.HasKey(t => t.Id);
                e.Property(t => t.Id).ValueGeneratedNever();   // jer u bazi već postoji GUID
                e.Property(t => t.Name).HasMaxLength(200).IsRequired(false);
                e.Property(t => t.Hostname).HasMaxLength(200).IsRequired(false);
                e.Property(t => t.IsActive).HasDefaultValue(true);
                e.HasIndex(t => t.Hostname).IsUnique();
            });

            // === ApplicationUser (Identity + naša proširenja) ===
            builder.Entity<ApplicationUser>(e =>
            {
                // opcionalno ograničenja
                e.Property(u => u.DisplayName).HasMaxLength(256);

                // FK prema Tenantu (nullable, bez cascade delete)
                e.HasOne<Tenant>()
                 .WithMany()
                 .HasForeignKey(u => u.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);

                // korisno za brze upite po tenantu
                e.HasIndex(u => u.TenantId);
            });

            // === TblTest (ostavljeno kakvo jest) ===
            builder.Entity<TblTest>(entity =>
            {
                entity.ToTable("tbl_Test");
                entity.Property(e => e.Id).ValueGeneratedNever().HasColumnName("ID");
                entity.Property(e => e.Name).HasMaxLength(10).IsFixedLength();
            });
        }
    }
}
