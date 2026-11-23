using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Config.Settings.Common;
using Ipm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Config.Settings.Queries.GetSettings;

internal sealed class GetSettingsQueryHandler(IApplicationDbContext db) : IQueryHandler<GetSettingsQuery, SettingsResponse>
{
    public async ValueTask<Result<SettingsResponse>> Handle(GetSettingsQuery query, CancellationToken cancellationToken) =>
        (await db.Settings.AsNoTracking().SingleAsync(cancellationToken)).ToResponse();
}
