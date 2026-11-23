using Ipm.Api.Infrastructure;
using Ipm.Application.Features.Search.Results.Queries.Search;
using Mediator;

namespace Ipm.Api.Endpoints.Search.Results.Search;

internal sealed class SearchEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("search", async (ISender sender, CancellationToken ct, string? q = null, int limit = 12) =>
                (await sender.Send(new SearchQuery(q, limit), ct)).ToHttpResult())
            .WithTags(SearchTags.Search)
            .WithSummary("البحث الشامل")
            .WithDescription("مشاريع وعناصر عمل وطلبات موافقة (ومستخدمون لمن يملك roles.manage) بتطابق النص، مفلترة بصلاحياتك. أقل من حرفين ← قائمة فاضية. الرمز الدقيق (ERP-104) أولاً. limit افتراضي 12 وأقصى 30. kind: project أو work-item أو approval أو user.");
}
