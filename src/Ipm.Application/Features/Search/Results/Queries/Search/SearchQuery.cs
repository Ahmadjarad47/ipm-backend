using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Search.Results.Common;

namespace Ipm.Application.Features.Search.Results.Queries.Search;

/// <summary>Spec 09 R12 to R15: projects, work items, approval requests (and users, for those who manage roles) matching the text, filtered by what the caller may see.</summary>
public sealed record SearchQuery(string? Q, int Limit) : IQuery<IReadOnlyList<SearchHit>>;
