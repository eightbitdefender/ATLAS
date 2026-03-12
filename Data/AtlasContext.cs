using Microsoft.EntityFrameworkCore;
using ATLAS.Models;

namespace ATLAS.Data;

public class AtlasContext : DbContext
{
    public AtlasContext(DbContextOptions<AtlasContext> options) : base(options) { }

    public DbSet<Asset>              Assets               => Set<Asset>();
    public DbSet<Vulnerability>      Vulnerabilities      => Set<Vulnerability>();
    public DbSet<AssetVulnerability> AssetVulnerabilities => Set<AssetVulnerability>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── TPH (Table-Per-Hierarchy) inheritance ────────────────────────────
        // All asset subtypes live in the single "Assets" table.
        // EF Core writes the concrete class name into the "Discriminator" column.
        modelBuilder.Entity<Asset>()
            .HasDiscriminator<string>("Discriminator")
            .HasValue<Computer>            ("Computer")
            .HasValue<NetworkDevice>       ("NetworkDevice")
            .HasValue<Printer>             ("Printer")
            .HasValue<SoftwareApplication> ("SoftwareApplication")
            .HasValue<MobileDevice>        ("MobileDevice")
            .HasValue<CloudResource>       ("CloudResource");

        // ── AssetVulnerability relationships ─────────────────────────────────
        modelBuilder.Entity<AssetVulnerability>()
            .HasOne(av => av.Asset)
            .WithMany(a => a.AssetVulnerabilities)
            .HasForeignKey(av => av.AssetId);

        modelBuilder.Entity<AssetVulnerability>()
            .HasOne(av => av.Vulnerability)
            .WithMany(v => v.AssetVulnerabilities)
            .HasForeignKey(av => av.VulnerabilityId);
    }
}
