using Ipm.Application.Features.Work.WorkItems.Commands.BulkUpdateWorkItems;

namespace Ipm.Api.Endpoints.Work.WorkItems.BulkUpdateWorkItems;

public sealed record BulkUpdateWorkItemsRequest(IReadOnlyList<Guid> Ids, BulkAction Action);
