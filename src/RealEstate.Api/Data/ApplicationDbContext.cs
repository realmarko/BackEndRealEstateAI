using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingAddress> ListingAddresses => Set<ListingAddress>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();
    public DbSet<ListingPriceHistory> ListingPriceHistories => Set<ListingPriceHistory>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<AgebPopulation> AgebPopulations => Set<AgebPopulation>();
    public DbSet<MunicipalBoundary> MunicipalBoundaries => Set<MunicipalBoundary>();
    public DbSet<MexicanState> MexicanStates => Set<MexicanState>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
    public DbSet<Fraccionamiento> Fraccionamientos => Set<Fraccionamiento>();
    public DbSet<FraccionamientoSource> FraccionamientoSources => Set<FraccionamientoSource>();
    public DbSet<LandUseCategory> LandUseCategories => Set<LandUseCategory>();
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();
    public DbSet<PipelineStageDocumentTemplate> PipelineStageDocumentTemplates => Set<PipelineStageDocumentTemplate>();
    public DbSet<SaleProcess> SaleProcesses => Set<SaleProcess>();
    public DbSet<SaleProcessDocument> SaleProcessDocuments => Set<SaleProcessDocument>();
    public DbSet<SaleProcessTask> SaleProcessTasks => Set<SaleProcessTask>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Listing>(entity =>
        {
            entity.Property(l => l.Price).HasColumnType("numeric(14,2)");
            entity.Property(l => l.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("MXN");
            entity.Property(l => l.Bathrooms).HasColumnType("numeric(4,1)");
            entity.HasIndex(l => new { l.Latitude, l.Longitude });

            entity.HasOne(l => l.Owner)
                  .WithMany(u => u.Listings)
                  .HasForeignKey(l => l.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Address)
                  .WithOne(a => a.Listing)
                  .HasForeignKey<ListingAddress>(a => a.ListingId)
                  .IsRequired()
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(l => l.Images)
                  .WithOne(i => i.Listing)
                  .HasForeignKey(i => i.ListingId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(l => l.PriceHistory)
                  .WithOne(h => h.Listing)
                  .HasForeignKey(h => h.ListingId)
                  .OnDelete(DeleteBehavior.Cascade);

            // SetNull, not Cascade: deleting (or un-publishing away) a Fraccionamiento record must
            // never take real, independently-owned listings down with it — the lot/house just
            // stops being associated with a development.
            entity.HasOne(l => l.Fraccionamiento)
                  .WithMany(f => f.Listings)
                  .HasForeignKey(l => l.FraccionamientoId)
                  .OnDelete(DeleteBehavior.SetNull);

            // SetNull, not Cascade/Restrict: the catalog is a fixed, admin-only lookup list, not
            // something a listing should be able to block from ever changing.
            entity.HasOne(l => l.LandUseCategory)
                  .WithMany()
                  .HasForeignKey(l => l.LandUseCategoryId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<LandUseCategory>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(50).IsRequired();

            // Seeded once, in order, so the migration's inserted Ids are stable and match the
            // fixed set of options the "Uso de suelo" dropdown offers (see
            // AddLandUseCategoryCatalog migration) — never user-editable at runtime.
            entity.HasData(
                new LandUseCategory { Id = 1, Name = "Urbano" },
                new LandUseCategory { Id = 2, Name = "Urbanizable" },
                new LandUseCategory { Id = 3, Name = "No urbanizable" },
                new LandUseCategory { Id = 4, Name = "Industrial" },
                new LandUseCategory { Id = 5, Name = "Residencial" },
                new LandUseCategory { Id = 6, Name = "Comercial" },
                new LandUseCategory { Id = 7, Name = "Agrícola" }
            );
        });

        builder.Entity<ListingAddress>(entity =>
        {
            entity.HasKey(a => a.ListingId);
            entity.Property(a => a.Street).HasMaxLength(300).IsRequired();
            entity.Property(a => a.Colonia).HasMaxLength(150).IsRequired();
            entity.Property(a => a.City).HasMaxLength(100).IsRequired();
            entity.Property(a => a.State).HasMaxLength(100).IsRequired();
            entity.Property(a => a.ZipCode).HasMaxLength(20).IsRequired();
            entity.Property(a => a.Country).HasMaxLength(100).IsRequired();
            entity.HasIndex(a => a.City);
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

        builder.Entity<MunicipalBoundary>(entity =>
        {
            entity.HasKey(m => m.Cvegeo);
            entity.Property(m => m.Cvegeo).HasMaxLength(5);
            entity.Property(m => m.Name).HasMaxLength(100);
            entity.Property(m => m.StateName).HasMaxLength(100);
            entity.HasIndex(m => m.Boundary).HasMethod("GIST");
        });

        builder.Entity<MexicanState>(entity =>
        {
            entity.HasKey(s => s.Code);
            entity.Property(s => s.Code).HasMaxLength(2);
            entity.Property(s => s.Name).HasMaxLength(100);
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

        builder.Entity<Fraccionamiento>(entity =>
        {
            // Backs both the admin queue's status filter and the ingestion endpoint's
            // dedup lookup (status + a proximity search over lat/lng).
            entity.HasIndex(f => f.Status);
            entity.HasIndex(f => new { f.Latitude, f.Longitude });

            entity.HasMany(f => f.Sources)
                  .WithOne(s => s.Fraccionamiento)
                  .HasForeignKey(s => s.FraccionamientoId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PipelineStage>(entity =>
        {
            entity.Property(s => s.Name).HasMaxLength(100).IsRequired();
            entity.Property(s => s.Description).HasMaxLength(300).IsRequired();
            entity.HasIndex(s => s.SortOrder).IsUnique();

            entity.HasMany(s => s.DocumentTemplates)
                  .WithOne(t => t.Stage)
                  .HasForeignKey(t => t.StageId)
                  .OnDelete(DeleteBehavior.Cascade);

            // The sale pipeline's fixed set of steps, in order. Ids 4 and 7 are the two
            // contract-review checkpoints (exclusivity contract, then compraventa/notaría) that
            // the frontend flags for legal sign-off before the agent moves the deal onward.
            entity.HasData(
                new PipelineStage { Id = 1, Name = "Prospección", Description = "Captación del cliente", SortOrder = 1, RequiresLegalReview = false },
                new PipelineStage { Id = 2, Name = "Revisión documental", Description = "Legal / propiedad", SortOrder = 2, RequiresLegalReview = false },
                new PipelineStage { Id = 3, Name = "Contrato de exclusividad", Description = "Firma con el vendedor", SortOrder = 3, RequiresLegalReview = false },
                new PipelineStage { Id = 4, Name = "Revisión de contrato (1)", Description = "Validación legal exclusividad", SortOrder = 4, RequiresLegalReview = true },
                new PipelineStage { Id = 5, Name = "Promoción y visitas", Description = "Marketing activo", SortOrder = 5, RequiresLegalReview = false },
                new PipelineStage { Id = 6, Name = "Negociación y oferta", Description = "Oferta del comprador", SortOrder = 6, RequiresLegalReview = false },
                new PipelineStage { Id = 7, Name = "Revisión de contrato (2)", Description = "Compraventa / notaría", SortOrder = 7, RequiresLegalReview = true },
                new PipelineStage { Id = 8, Name = "Cierre y escrituración", Description = "Firma final", SortOrder = 8, RequiresLegalReview = false },
                new PipelineStage { Id = 9, Name = "Post-venta", Description = "Entrega y seguimiento", SortOrder = 9, RequiresLegalReview = false }
            );
        });

        builder.Entity<PipelineStageDocumentTemplate>(entity =>
        {
            entity.Property(t => t.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(t => new { t.StageId, t.Name }).IsUnique();

            // Default checklist per stage — mirrors the pipeline prototype's STAGE_DOCS. Copied
            // (unverified) into SaleProcessDocument by SaleProcessService the first time a
            // SaleProcess reaches that stage.
            entity.HasData(
                new PipelineStageDocumentTemplate { Id = 1, StageId = 1, SortOrder = 1, Name = "Identificación oficial del propietario" },
                new PipelineStageDocumentTemplate { Id = 2, StageId = 1, SortOrder = 2, Name = "Comprobante de domicilio" },
                new PipelineStageDocumentTemplate { Id = 3, StageId = 1, SortOrder = 3, Name = "Ficha de datos del inmueble" },

                new PipelineStageDocumentTemplate { Id = 4, StageId = 2, SortOrder = 1, Name = "Escritura pública inscrita" },
                new PipelineStageDocumentTemplate { Id = 5, StageId = 2, SortOrder = 2, Name = "Boleta predial al corriente" },
                new PipelineStageDocumentTemplate { Id = 6, StageId = 2, SortOrder = 3, Name = "Certificado de libertad de gravamen" },
                new PipelineStageDocumentTemplate { Id = 7, StageId = 2, SortOrder = 4, Name = "Comprobante de servicios (agua/luz)" },

                new PipelineStageDocumentTemplate { Id = 8, StageId = 3, SortOrder = 1, Name = "Contrato de exclusividad firmado" },
                new PipelineStageDocumentTemplate { Id = 9, StageId = 3, SortOrder = 2, Name = "Identificación de quien firma" },
                new PipelineStageDocumentTemplate { Id = 10, StageId = 3, SortOrder = 3, Name = "Formato KYC / prevención de lavado" },

                new PipelineStageDocumentTemplate { Id = 11, StageId = 4, SortOrder = 1, Name = "Dictamen del abogado sobre el contrato de exclusividad" },
                new PipelineStageDocumentTemplate { Id = 12, StageId = 4, SortOrder = 2, Name = "Acta de revisión de cláusulas" },

                new PipelineStageDocumentTemplate { Id = 13, StageId = 5, SortOrder = 1, Name = "Ficha técnica comercial" },
                new PipelineStageDocumentTemplate { Id = 14, StageId = 5, SortOrder = 2, Name = "Fotografías / video del inmueble" },
                new PipelineStageDocumentTemplate { Id = 15, StageId = 5, SortOrder = 3, Name = "Autorización de publicación" },

                new PipelineStageDocumentTemplate { Id = 16, StageId = 6, SortOrder = 1, Name = "Carta de oferta del comprador" },
                new PipelineStageDocumentTemplate { Id = 17, StageId = 6, SortOrder = 2, Name = "Comprobante de solvencia / precalificación crédito" },

                new PipelineStageDocumentTemplate { Id = 18, StageId = 7, SortOrder = 1, Name = "Minuta de compraventa revisada por notaría" },
                new PipelineStageDocumentTemplate { Id = 19, StageId = 7, SortOrder = 2, Name = "Dictamen del abogado sobre el contrato de compraventa" },
                new PipelineStageDocumentTemplate { Id = 20, StageId = 7, SortOrder = 3, Name = "Carta saldo / cancelación de hipoteca (si aplica)" },

                new PipelineStageDocumentTemplate { Id = 21, StageId = 8, SortOrder = 1, Name = "Avalúo comercial oficial" },
                new PipelineStageDocumentTemplate { Id = 22, StageId = 8, SortOrder = 2, Name = "Hoja de retención ISR" },
                new PipelineStageDocumentTemplate { Id = 23, StageId = 8, SortOrder = 3, Name = "Escritura de compraventa firmada" },

                new PipelineStageDocumentTemplate { Id = 24, StageId = 9, SortOrder = 1, Name = "Acta de entrega-recepción" },
                new PipelineStageDocumentTemplate { Id = 25, StageId = 9, SortOrder = 2, Name = "Carta garantía / finiquito" },
                new PipelineStageDocumentTemplate { Id = 26, StageId = 9, SortOrder = 3, Name = "Encuesta de satisfacción" }
            );
        });

        builder.Entity<SaleProcess>(entity =>
        {
            entity.Property(s => s.ClientName).HasMaxLength(200).IsRequired();
            entity.Property(s => s.ClientPhone).HasMaxLength(30).IsRequired();
            entity.Property(s => s.PropertyAddress).HasMaxLength(300).IsRequired();
            entity.Property(s => s.EstimatedPrice).HasColumnType("numeric(14,2)");
            entity.HasIndex(s => s.AgentUserId);
            entity.HasIndex(s => s.CurrentStageId);

            entity.HasOne(s => s.AgentUser)
                  .WithMany()
                  .HasForeignKey(s => s.AgentUserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // SetNull, not Cascade/Restrict: removing or un-publishing the Listing must never
            // take the sale process (and its history/checklist/tasks) down with it.
            entity.HasOne(s => s.Listing)
                  .WithMany()
                  .HasForeignKey(s => s.ListingId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Restrict, not SetNull/Cascade: the stage catalog is fixed and never deleted, so
            // this only guards against a future catalog edit accidentally removing a stage still
            // referenced by live sale processes.
            entity.HasOne(s => s.CurrentStage)
                  .WithMany()
                  .HasForeignKey(s => s.CurrentStageId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SaleProcessDocument>(entity =>
        {
            entity.Property(d => d.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(d => d.SaleProcessId);

            entity.HasOne(d => d.SaleProcess)
                  .WithMany(s => s.Documents)
                  .HasForeignKey(d => d.SaleProcessId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Stage)
                  .WithMany()
                  .HasForeignKey(d => d.StageId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SaleProcessTask>(entity =>
        {
            entity.Property(t => t.Title).HasMaxLength(300).IsRequired();
            entity.HasIndex(t => t.SaleProcessId);

            entity.HasOne(t => t.SaleProcess)
                  .WithMany(s => s.Tasks)
                  .HasForeignKey(t => t.SaleProcessId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
