using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Data;

public class RealEstateDbContext : DbContext
{
    public RealEstateDbContext(DbContextOptions<RealEstateDbContext> options) : base(options) { }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Agent> Agents => Set<Agent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
        });
    }
}
