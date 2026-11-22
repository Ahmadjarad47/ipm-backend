using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Workflows.Common;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Workflows.Queries.GetWorkflows;

/// <summary>Every workflow, optionally of one scope. No paging: this is a short list of settings.</summary>
public sealed record GetWorkflowsQuery(StatusScope? Scope) : IQuery<IReadOnlyList<WorkflowResponse>>;
