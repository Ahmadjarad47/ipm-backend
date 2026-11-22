using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Statuses.Common;
using Ipm.Domain.Config;

namespace Ipm.Application.Features.Config.Statuses.Queries.GetStatuses;

/// <summary>Every status, in display order, optionally of one scope. No paging: this is a short list of settings.</summary>
public sealed record GetStatusesQuery(StatusScope? Scope, bool? Active) : IQuery<IReadOnlyList<StatusResponse>>;
