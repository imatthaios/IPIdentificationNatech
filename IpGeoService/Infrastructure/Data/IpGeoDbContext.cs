using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class IpGeoDbContext : DbContext
{
    public IpGeoDbContext(DbContextOptions<IpGeoDbContext> options) : base(options) { }

    public DbSet<Batch> Batches { get; set; } = null!;
    public DbSet<BatchItem> BatchItems { get; set; } = null!;
    public DbSet<IpGeoCache> IpGeoCache { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Batch
        modelBuilder.Entity<Batch>(b =>
        {
            b.ToTable("Batches");
            b.HasKey(x => x.Id);

            b.Property(x => x.Status)
                .HasConversion<int>();

            b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
            b.Property(x => x.StartedAtUtc).HasColumnType("datetime2");
            b.Property(x => x.CompletedAtUtc).HasColumnType("datetime2");

            b.Property(x => x.TotalCount).IsRequired();
            b.Property(x => x.ProcessedCount).IsRequired();

            b.HasMany(x => x.Items)
                .WithOne(i => i.Batch)
                .HasForeignKey(i => i.BatchId);
        });

        modelBuilder.Entity<BatchItem>(bi =>
        {
            bi.ToTable("BatchItems");
            bi.HasKey(x => x.Id);

            bi.Property(x => x.Ip)
                .IsRequired()
                .HasMaxLength(45);

            bi.Property(x => x.Status)
                .HasConversion<int>();

            bi.Property(x => x.StartedAtUtc).HasColumnType("datetime2");
            bi.Property(x => x.CompletedAtUtc).HasColumnType("datetime2");

            bi.HasIndex(x => x.BatchId);
            bi.HasIndex(x => x.Ip);
        });

        modelBuilder.Entity<IpGeoCache>(c =>
        {
            c.ToTable("IPGeoCache");
            c.HasKey(x => x.Ip);
            c.Property(x => x.Ip).HasMaxLength(45);
            c.Property(x => x.LastFetchedUtc).HasColumnType("datetime2");
            c.HasIndex(x => x.LastFetchedUtc);
        });
    }
}
