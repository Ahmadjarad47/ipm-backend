using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Projects.Projects.Common;

namespace Ipm.Application.Features.Projects.Projects.Commands.SetProjectWorkItemTypes;

/// <summary>Replaces the work item types the project allows and its default type (R12).</summary>
public sealed record SetProjectWorkItemTypesCommand(Guid Id, IReadOnlyList<Guid> AllowedTypeIds, Guid DefaultTypeId, uint ExpectedVersion)
    : ICommand<ProjectResponse>;
