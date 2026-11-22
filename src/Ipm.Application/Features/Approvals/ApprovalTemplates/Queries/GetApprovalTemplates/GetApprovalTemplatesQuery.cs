using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Common.Pagination;
using Ipm.Application.Features.Approvals.ApprovalTemplates.Common;

namespace Ipm.Application.Features.Approvals.ApprovalTemplates.Queries.GetApprovalTemplates;

/// <summary>The approval templates, by name. <paramref name="Active"/> keeps only the active (or only the inactive) ones.</summary>
public sealed record GetApprovalTemplatesQuery(bool? Active, PageRequest Page) : IQuery<PagedList<ApprovalTemplateResponse>>;
