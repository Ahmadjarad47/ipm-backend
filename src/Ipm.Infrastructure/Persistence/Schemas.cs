namespace Ipm.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL schema per business module. Each part adds its constant here and uses it as
/// <c>builder.ToTable("projects", Schemas.Projects)</c> in entity configurations.
/// Part 00 owns no business schema: the audit log lives in <c>public</c>.
/// </summary>
public static class Schemas
{
}
