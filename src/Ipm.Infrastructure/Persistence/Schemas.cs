namespace Ipm.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL schema per business module. Each part adds its constant here and uses it as
/// <c>builder.ToTable("projects", Schemas.Projects)</c> in entity configurations.
/// The audit log lives in <c>public</c>.
/// </summary>
public static class Schemas
{
    public const string Identity = "identity";
    public const string Config = "config";
    public const string Projects = "projects";
    public const string Work = "work";
    public const string Approvals = "approvals";
}
