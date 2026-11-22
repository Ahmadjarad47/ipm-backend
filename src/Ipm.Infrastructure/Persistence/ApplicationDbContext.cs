using Ipm.Application.Abstractions.Data;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context over PostgreSQL. Each business module maps its tables in its own schema
/// through IEntityTypeConfiguration classes (discovered from this assembly). Part 01 turns it into the
/// ASP.NET Core Identity store.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IUnitOfWork, IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssemblyReference).Assembly);
        ApplyEntityConventions(modelBuilder);
    }

    /// <summary>
    /// Rules every <see cref="Entity"/> gets without repeating them in each configuration:
    /// <c>Version</c> is PostgreSQL's <c>xmin</c> (optimistic concurrency), domain events are not columns.
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
