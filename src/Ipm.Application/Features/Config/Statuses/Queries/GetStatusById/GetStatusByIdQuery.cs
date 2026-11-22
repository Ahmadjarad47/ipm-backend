using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Statuses.Common;

namespace Ipm.Application.Features.Config.Statuses.Queries.GetStatusById;

public sealed record GetStatusByIdQuery(Guid Id) : IQuery<StatusResponse>;
