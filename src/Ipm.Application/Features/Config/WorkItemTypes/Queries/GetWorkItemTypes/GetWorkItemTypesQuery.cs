using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;

namespace Ipm.Application.Features.Config.WorkItemTypes.Queries.GetWorkItemTypes;

/// <summary>Every work item type, in display order. No paging: this is a short list of settings.</summary>
public sealed record GetWorkItemTypesQuery(bool? Active) : IQuery<IReadOnlyList<WorkItemTypeResponse>>;
