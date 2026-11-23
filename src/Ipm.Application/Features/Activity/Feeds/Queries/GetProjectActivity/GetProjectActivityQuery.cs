using Ipm.Application.Abstractions.Auditing;
using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;

namespace Ipm.Application.Features.Activity.Feeds.Queries.GetProjectActivity;

/// <summary>Spec 08 R9: what happened in a project (its own changes and those of everything in it), newest first. Security events never appear.</summary>
public sealed record GetProjectActivityQuery(Guid ProjectId, PageRequest Page) : IQuery<PagedList<AuditEventResponse>>;
