using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Work.WorkItems.Common;

namespace Ipm.Application.Features.Work.WorkItems.Queries.GetWorkItemById;

/// <summary>A work item of a project the caller can see (tasks.view). Any other answers as if it did not exist (404).</summary>
public sealed record GetWorkItemByIdQuery(Guid Id) : IQuery<WorkItemResponse>;
