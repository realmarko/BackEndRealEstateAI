using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Data;

public class RealEstateDbContext : DbContext
{
    public RealEstateDbContext(DbContextOptions<RealEstateDbContext> options) : base(options) { }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentReview> AgentReviews => Set<AgentReview>();
    public DbSet<Brokerage> Brokerages => Set<Brokerage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Agent>(entity =>
        {
            entity.HasIndex(a => a.UserId).IsUnique().HasFilter("user_id IS NOT NULL");
            entity.Property(a => a.Specialties).HasDefaultValueSql("'{}'");
        });

        builder.Entity<AgentReview>(entity =>
        {
            // One review per reviewer per agent.
            entity.HasIndex(r => new { r.AgentId, r.ReviewerUserId }).IsUnique();

            entity.HasOne(r => r.Agent)
                  .WithMany(a => a.Reviews)
                  .HasForeignKey(r => r.AgentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Brokerage>(entity =>
        {
            entity.HasIndex(b => b.Name).IsUnique();
        });

        builder.Entity<Property>(entity =>
        {
            entity.Property(p => p.Price).HasColumnType("numeric(14,2)");
            entity.HasIndex(p => p.City);
            entity.HasIndex(p => new { p.Latitude, p.Longitude });

            entity.HasOne(p => p.Agent)
                  .WithMany(a => a.Properties)
                  .HasForeignKey(p => p.AgentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(p => p.Images)
                  .WithOne(i => i.Property)
                  .HasForeignKey(i => i.PropertyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.Amenities)
                  .WithMany(a => a.Properties)
                  .UsingEntity(j => j.ToTable("PropertyAmenities"));
        });
    }
}
