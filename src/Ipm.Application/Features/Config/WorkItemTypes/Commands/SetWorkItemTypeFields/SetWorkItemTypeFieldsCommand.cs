using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.WorkItemTypes.Commands.SetWorkItemTypeFields;

/// <summary>Replaces the whole list of custom fields of the type (R3).</summary>
public sealed record SetWorkItemTypeFieldsCommand(Guid Id, IReadOnlyList<FieldDefinition> Fields, uint ExpectedVersion)
    : ICommand<WorkItemTypeResponse>;
