using KarlixID.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KarlixID.Web.Data
{
    // VAŽNO: koristimo IdentityDbContext<ApplicationUser>
    public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // Tvoje domenske tablice:
        public virtual DbSet<Tenant> Tenants { get; set; } = null!;
        public virtual DbSet<TblTest> TblTests { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // zadrži Identity konfiguraciju

            // tbl_Test
            modelBuilder.Entity<TblTest>(entity =>
            {
                entity.ToTable("tbl_Test");
                entity.Property(e => e.Id)
                      .ValueGeneratedNever()
                      .HasColumnName("ID");
                entity.Property(e => e.Name)
                      .HasMaxLength(10)
                      .IsFixedLength();
            });

            // Tenants
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK__Tenants__3214EC07");
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.Hostname).HasMaxLength(200);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasIndex(e => e.Hostname).IsUnique();
            });

            // Ako želiš dodatnu konfiguraciju za ApplicationUser (npr. TenantId not null/index):
            // modelBuilder.Entity<ApplicationUser>(e =>
            // {
            //     e.HasIndex(u => u.TenantId);
            //     // e.Property(u => u.TenantId).IsRequired(); // ako u bazi nije NULL
            // });
        }
    }
}
