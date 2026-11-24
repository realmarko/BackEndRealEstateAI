using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Data;

public class RealEstateDbContext : DbContext
{
    public RealEstateDbContext(DbContextOptions<RealEstateDbContext> options) : base(options) { }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentBrokerage> AgentBrokerages => Set<AgentBrokerage>();
    public DbSet<AgentReview> AgentReviews => Set<AgentReview>();
    public DbSet<Owner> Owners => Set<Owner>();
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

        builder.Entity<AgentBrokerage>(entity =>
        {
            entity.HasKey(ab => new { ab.AgentId, ab.BrokerageId });

            // Restrict (not Cascade): deleting a brokerage from the catalog while agents still
            // belong to it should fail loudly, not silently orphan/delete those agents — unlike
            // the old single-FK version, there's no "fall back to independent" here since an
            // agent can still have other memberships left.
            entity.HasOne(ab => ab.Agent)
                  .WithMany(a => a.AgentBrokerages)
                  .HasForeignKey(ab => ab.AgentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ab => ab.Brokerage)
                  .WithMany(b => b.AgentBrokerages)
                  .HasForeignKey(ab => ab.BrokerageId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Owner>(entity =>
        {
            entity.HasIndex(o => o.UserId).IsUnique().HasFilter("user_id IS NOT NULL");
            // Backstops ListingsController.UpdateOwnerContact's find-or-create-by-email against a
            // concurrent duplicate insert (two admins/listings racing the same new owner's email) —
            // same shape as the UserId index above. Filtered because Email defaults to "" for rows
            // that somehow never got one; empty string shouldn't be "unique" across them.
            entity.HasIndex(o => o.Email).IsUnique().HasFilter("email <> ''");
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
            // Declared here so `dotnet ef migrations add` keeps seeing "unique index on Name"
            // as unchanged and never tries to touch it. The actual index in the database is a
            // case-insensitive one (unique on LOWER(name)), swapped in via raw SQL in the
            // AddAgentBrokerageId migration — EF's fluent API has no first-class way to express
            // a Postgres expression index, and this line can't be safely edited to match without
            // scaffolding a spurious drop/recreate of that functional index.
            entity.HasIndex(b => b.Name).IsUnique();
        });

        builder.Entity<Property>(entity =>
        {
            entity.Property(p => p.Price).HasColumnType("numeric(14,2)");
            entity.HasIndex(p => p.City);
            entity.HasIndex(p => new { p.Latitude, p.Longitude });

            entity.HasOne(p => p.Agent)
                  .WithMany()
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
