using Ipm.Domain.Common;
using Ipm.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Ipm.IntegrationTests.Probe;

/// <summary>
/// Test-only entity, never part of the real model or migrations. Lets the foundation tests check audit,
/// optimistic concurrency and error responses before any real table exists.
/// </summary>
public sealed class ProbeItem : AuditableEntity
{
    private ProbeItem() { }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Stands in for a password hash: must never reach audit_log.</summary>
    public string Secret { get; private set; } = string.Empty;

    public static ProbeItem Create(string name) => new() { Name = name, Secret = "hidden-" + name };

    public void Rename(string name) => Name = name;
}

/// <summary>Adds <see cref="ProbeItem"/> to the model, configured like a real part would.</summary>
public sealed class ProbeModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        modelBuilder.Entity<ProbeItem>(item =>
        {
            item.ToTable("probe_items", "test");
            item.HasKey(e => e.Id);
            item.Property(e => e.Name).HasMaxLength(100);
            item.Property(e => e.Secret).HasMaxLength(100).ExcludeFromAudit();

            // ApplicationDbContext applies these to every Entity, but this type is added after that runs.
            item.Ignore(e => e.DomainEvents);
            item.Property(e => e.Version).IsRowVersion();
        });
    }
}
