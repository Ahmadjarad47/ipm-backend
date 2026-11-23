using Ipm.Application.Abstractions.Messaging;
using Ipm.Application.Features.Dashboard.Stats.Common;

namespace Ipm.Application.Features.Dashboard.Stats.Queries.GetDashboardStats;

/// <summary>Spec 09 R8 to R10. <paramref name="From"/> and <paramref name="To"/> are days in the organization time zone; <paramref name="StatusCategory"/> is a comma list of kebab categories.</summary>
public sealed record GetDashboardStatsQuery(Guid? ProjectId, DateOnly? From, DateOnly? To, string? StatusCategory, Guid? AssigneeId) : IQuery<DashboardStatsResponse>;
