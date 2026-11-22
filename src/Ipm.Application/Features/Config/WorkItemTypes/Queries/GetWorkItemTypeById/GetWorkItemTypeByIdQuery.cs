using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.WorkItemTypes.Common;

namespace Ipm.Application.Features.Config.WorkItemTypes.Queries.GetWorkItemTypeById;

public sealed record GetWorkItemTypeByIdQuery(Guid Id) : IQuery<WorkItemTypeResponse>;
