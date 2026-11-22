namespace Ipm.Api.Endpoints.Work.Relations.CreateRelation;

public sealed record CreateRelationRequest(Guid SourceId, Guid TargetId, Guid RelationTypeId, uint? SourceVersion, uint? TargetVersion);
