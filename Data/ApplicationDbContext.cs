using System;
using KarlixID.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict; // ⬅️ zbog modelBuilder.UseOpenIddict()

namespace KarlixID.Web.Data
{
    // Jedan kontekst: Identity + tvoje poslovne tablice
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Poslovne tablice
        public DbSet<Tenant> Tenants { get; set; } = null!;
        public DbSet<Invite> Invites { get; set; } = null!;
        public DbSet<TblTest> TblTests { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ➜ Uključi OpenIddict EF model (OpenIddict* tablice)
            builder.UseOpenIddict();

            // === Identity: mapiranje na postojeće tablice/indekse/duljine ===
            builder.Entity<ApplicationUser>(b =>
            {
                b.ToTable("AspNetUsers");

                // dodatna polja
                b.Property(u => u.DisplayName).HasMaxLength(256);

                // postojeći indeksi/ograničenja iz tvoje baze
                b.Property(u => u.UserName).HasMaxLength(256);
                b.Property(u => u.NormalizedUserName).HasMaxLength(256);
                b.Property(u => u.Email).HasMaxLength(256);
                b.Property(u => u.NormalizedEmail).HasMaxLength(256);

                b.HasIndex(u => u.NormalizedEmail)
                    .HasDatabaseName("IX_AspNetUsers_NormalizedEmail")
                    .IsUnique()
                    .HasFilter("([NormalizedEmail] IS NOT NULL)");

                b.HasIndex(u => u.NormalizedUserName)
                    .HasDatabaseName("IX_AspNetUsers_NormalizedUserName")
                    .IsUnique()
                    .HasFilter("([NormalizedUserName] IS NOT NULL)");

                b.HasIndex(u => u.TenantId)
                    .HasDatabaseName("IX_AspNetUsers_TenantId");
            });

            builder.Entity<IdentityRole>(b =>
            {
                b.ToTable("AspNetRoles");
                b.Property(r => r.Name).HasMaxLength(256);
                b.Property(r => r.NormalizedName).HasMaxLength(256);

                b.HasIndex(r => r.NormalizedName)
                    .HasDatabaseName("IX_AspNetRoles_NormalizedName")
                    .IsUnique()
                    .HasFilter("([NormalizedName] IS NOT NULL)");
            });

            builder.Entity<IdentityUserClaim<string>>(b =>
            {
                b.ToTable("AspNetUserClaims");
            });

            builder.Entity<IdentityUserLogin<string>>(b =>
            {
                b.ToTable("AspNetUserLogins");
                // u tvojoj bazi su 128
                b.Property(l => l.LoginProvider).HasMaxLength(128);
                b.Property(l => l.ProviderKey).HasMaxLength(128);
            });

            builder.Entity<IdentityUserToken<string>>(b =>
            {
                b.ToTable("AspNetUserTokens");
                // u tvojoj bazi su 128
                b.Property(t => t.LoginProvider).HasMaxLength(128);
                b.Property(t => t.Name).HasMaxLength(128);
            });

            builder.Entity<IdentityRoleClaim<string>>(b =>
            {
                b.ToTable("AspNetRoleClaims");
            });

            builder.Entity<IdentityUserRole<string>>(b =>
            {
                b.ToTable("AspNetUserRoles");
                // primarni ključ i indexe EF će sam složiti
            });

            // === Tenants ===
            builder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasKey(e => e.Id).HasName("PK__Tenants__3214EC0727C97BFE");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.Hostname).HasMaxLength(200);
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                entity.HasIndex(e => e.Hostname)
                      .HasDatabaseName("UQ__Tenants__374DA02775CC57E9")
                      .IsUnique();
            });

            // === Invites ===
            builder.Entity<Invite>(entity =>
            {
                entity.ToTable("Invites");
                entity.HasKey(e => e.Id).HasName("PK__Invites__3214EC072F438260");

                entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
                entity.Property(e => e.Email).HasMaxLength(256);
                entity.Property(e => e.RoleName).HasMaxLength(100);
                entity.Property(e => e.Token).HasMaxLength(64); // 64-znakovni hex token
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
                entity.Property(e => e.CreatedBy).HasMaxLength(450);

                entity.HasIndex(e => e.Email).HasDatabaseName("IX_Invites_Email");
                entity.HasIndex(e => e.TenantId).HasDatabaseName("IX_Invites_TenantId");
                entity.HasIndex(e => e.Token)
                      .HasDatabaseName("UQ__Invites__1EB4F817182FBE2A")
                      .IsUnique();
            });

            // === tbl_Test ===
            builder.Entity<TblTest>(entity =>
            {
                entity.ToTable("tbl_Test");
                entity.Property(e => e.Id).ValueGeneratedNever().HasColumnName("ID");
                entity.Property(e => e.Name).HasMaxLength(10).IsFixedLength();
            });
        }
    }
}
