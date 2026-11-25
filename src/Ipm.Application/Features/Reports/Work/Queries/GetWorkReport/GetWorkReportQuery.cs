using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Reports.Work.Common;

namespace Ipm.Application.Features.Reports.Work.Queries.GetWorkReport;

/// <summary>
/// Spec 10. The work report of a period (days in the organization time zone), for the projects in <paramref name="ProjectIds"/> (empty: every project the caller may export)
/// and, when <paramref name="AssigneeId"/> is given, for one person's items only.
/// </summary>
public sealed record GetWorkReportQuery(DateOnly From, DateOnly To, IReadOnlyList<Guid> ProjectIds, Guid? AssigneeId) : IQuery<WorkReportFile>;
