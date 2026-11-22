namespace Ipm.Domain.Config;

/// <summary>An allowed (source type, target type) combination for a relation type (FR-REL-04).</summary>
public sealed record TypePair(Guid SourceTypeId, Guid TargetTypeId);
