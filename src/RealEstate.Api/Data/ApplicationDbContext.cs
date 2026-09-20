using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();
    public DbSet<ListingPriceHistory> ListingPriceHistories => Set<ListingPriceHistory>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<AgebPopulation> AgebPopulations => Set<AgebPopulation>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Listing>(entity =>
        {
            entity.Property(l => l.Price).HasColumnType("numeric(14,2)");
            entity.Property(l => l.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("MXN");
            entity.Property(l => l.Bathrooms).HasColumnType("numeric(4,1)");
            entity.HasIndex(l => l.City);
            entity.HasIndex(l => new { l.Latitude, l.Longitude });

            entity.HasOne(l => l.Owner)
                  .WithMany(u => u.Listings)
                  .HasForeignKey(l => l.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(l => l.Images)
                  .WithOne(i => i.Listing)
                  .HasForeignKey(i => i.ListingId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(l => l.PriceHistory)
                  .WithOne(h => h.Listing)
                  .HasForeignKey(h => h.ListingId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ListingPriceHistory>(entity =>
        {
            entity.Property(h => h.Price).HasColumnType("numeric(14,2)");
            entity.Property(h => h.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("MXN");
            entity.HasIndex(h => new { h.ListingId, h.RecordedAt });
        });

        builder.Entity<Favorite>(entity =>
        {
            entity.HasIndex(f => new { f.UserId, f.ListingId }).IsUnique();

            entity.HasOne(f => f.User)
                  .WithMany(u => u.Favorites)
                  .HasForeignKey(f => f.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.Listing)
                  .WithMany(l => l.Favorites)
                  .HasForeignKey(f => f.ListingId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Inquiry>(entity =>
        {
            entity.HasOne(i => i.Listing)
                  .WithMany(l => l.Inquiries)
                  .HasForeignKey(i => i.ListingId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Sender)
                  .WithMany()
                  .HasForeignKey(i => i.SenderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SavedSearch>(entity =>
        {
            entity.Property(s => s.MinPrice).HasColumnType("numeric(14,2)");
            entity.Property(s => s.MaxPrice).HasColumnType("numeric(14,2)");
            entity.HasIndex(s => s.UserId);

            entity.HasOne(s => s.User)
                  .WithMany(u => u.SavedSearches)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AgebPopulation>(entity =>
        {
            entity.HasKey(a => a.Cvegeo);
            entity.Property(a => a.Cvegeo).HasMaxLength(13);
            // GIST, not the default B-tree — required for PostGIS's ST_Contains to use an index
            // instead of scanning every AGEB polygon on every opportunity-analysis map click.
            entity.HasIndex(a => a.Boundary).HasMethod("GIST");
        });

        builder.Entity<ErrorLog>(entity =>
        {
            // No FK to ApplicationUser on purpose: an error log is an audit trail, not a live
            // relationship — it should survive (with UserEmail as the readable trace) even if the
            // account is later deleted, rather than being cascade-deleted or blocking the delete.
            entity.HasIndex(e => e.OccurredAt);
            entity.HasIndex(e => e.Resolved);
            // Backs ErrorsController's grouping GROUP BY, its four per-group "latest occurrence"
            // subqueries, and ResolveGroup's bulk WHERE — all filtered on this signature. Message
            // is deliberately left out of the index itself (Postgres's B-tree row-size limit is
            // ~2.7KB, and Message can run up to 2000 chars/~8KB worst case per ErrorsController's
            // own MaxMessageLength) — Source/Severity/Section already narrows to a small handful
            // of rows in practice, and Postgres filters the rest by Message with a plain scan over
            // that narrowed set instead of needing it in the index too.
            entity.HasIndex(e => new { e.Source, e.Severity, e.Section });
        });
    }
}
