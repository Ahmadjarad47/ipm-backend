using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Common;
using Ipm.Domain.Config;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Ipm.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context over PostgreSQL. It is also the ASP.NET Core Identity user store
/// (<see cref="User"/> is the user; we do not use Identity roles, our <see cref="Role"/> carries permissions).
/// Each business module maps its tables in its own schema through IEntityTypeConfiguration classes.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityUserContext<User, Guid>(options), IUnitOfWork, IApplicationDbContext
{
    public DbSet<Role> Roles => Set<Role>();

    public DbSet<WorkItemType> WorkItemTypes => Set<WorkItemType>();

    public DbSet<RelationType> RelationTypes => Set<RelationType>();

    public DbSet<Status> Statuses => Set<Status>();

    public DbSet<Workflow> Workflows => Set<Workflow>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<ProjectStatusChange> ProjectStatusChanges => Set<ProjectStatusChange>();

    public DbSet<ProjectTemplate> ProjectTemplates => Set<ProjectTemplate>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's own mapping first, then ours (which renames its tables into the identity schema).
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AssemblyReference).Assembly);
        MapIdentityTables(builder);
        ApplyEntityConventions(builder);
    }

    private static void MapIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims", Schemas.Identity);
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins", Schemas.Identity);
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens", Schemas.Identity);
    }

    /// <summary>
    /// Rules every <see cref="Entity"/> gets without repeating them in each configuration:
    /// <c>Version</c> is PostgreSQL xmin (optimistic concurrency), domain events are not columns.
    /// </summary>
    private static void ApplyEntityConventions(ModelBuilder modelBuilder)
    {
        var roots = modelBuilder.Model.GetEntityTypes()
            .Where(t => t.BaseType is null && !t.IsOwned() && t.ClrType.IsAssignableTo(typeof(Entity)))
            .Select(t => t.ClrType)
            .ToList();

        foreach (var type in roots)
        {
            var entity = modelBuilder.Entity(type);

            // Ids are UUIDv7 made by the domain, never by the database. Without this, EF treats a new child
            // added to a loaded collection as an existing row and issues an UPDATE.
            entity.Property(nameof(Entity.Id)).ValueGeneratedNever();
            entity.Ignore(nameof(Entity.DomainEvents));
            entity.Property(nameof(Entity.Version)).IsRowVersion();
        }
    }
}
