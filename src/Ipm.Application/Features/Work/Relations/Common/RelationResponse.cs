namespace Ipm.Application.Features.Work.Relations.Common;

/// <summary>One link between two work items, stored from the source to the target.</summary>
public sealed record RelationResponse(Guid Id, Guid SourceId, Guid TargetId, Guid RelationTypeId, Guid? CreatedBy, DateTimeOffset CreatedAt);
