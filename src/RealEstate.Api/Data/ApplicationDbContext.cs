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
    }
}
