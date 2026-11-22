using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.Relations.Common;

namespace Ipm.Application.Features.Work.Relations.Commands.CreateRelation;

/// <summary>Links <paramref name="SourceId"/> to <paramref name="TargetId"/> (R1 to R6). The versions are optional: when sent, they must still be the current ones.</summary>
public sealed record CreateRelationCommand(Guid SourceId, Guid TargetId, Guid RelationTypeId, uint? SourceVersion, uint? TargetVersion) : ICommand<RelationResponse>;
