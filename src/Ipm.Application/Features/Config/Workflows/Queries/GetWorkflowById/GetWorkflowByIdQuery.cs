using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Workflows.Common;

namespace Ipm.Application.Features.Config.Workflows.Queries.GetWorkflowById;

public sealed record GetWorkflowByIdQuery(Guid Id) : IQuery<WorkflowResponse>;
