namespace Ipm.Domain.Common;

/// <summary>Entities whose creation/modification stamps are filled automatically on save.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }
    Guid? CreatedBy { get; }
    DateTimeOffset? UpdatedAt { get; }
    Guid? UpdatedBy { get; }

    void SetCreated(DateTimeOffset at, Guid? by);
    void SetUpdated(DateTimeOffset at, Guid? by);
}

/// <summary>Convenience base class implementing <see cref="IAuditable"/>.</summary>
public abstract class AuditableEntity : Entity, IAuditable
{
    protected AuditableEntity(Guid id) : base(id) { }

    protected AuditableEntity() { }

    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public void SetCreated(DateTimeOffset at, Guid? by)
    {
        CreatedAt = at;
        CreatedBy = by;
    }

    public void SetUpdated(DateTimeOffset at, Guid? by)
    {
        UpdatedAt = at;
        UpdatedBy = by;
    }
}
