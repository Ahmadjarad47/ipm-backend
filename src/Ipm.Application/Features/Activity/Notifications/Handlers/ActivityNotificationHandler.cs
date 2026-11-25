using Ipm.Application.Abstractions.Data;
using Ipm.Application.Abstractions.Events;
using Ipm.Application.Features.Activity.Notifications.Common;
using Ipm.Domain.Activity;
using Ipm.Domain.Activity.Events;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ipm.Application.Features.Activity.Notifications.Handlers;

/// <summary>
/// Spec 08 R10 and R11: turns the domain events of the earlier parts into notifications. Each event is handled in a scope of its own (the
/// request context is already finished saving), and a failure here never reaches the request that raised the event (the dispatcher logs it).
/// </summary>
internal sealed class ActivityNotificationHandler(IServiceScopeFactory scopes) :
    IDomainEventHandler<WorkItemAssigneesChanged>,
    IDomainEventHandler<WorkItemStatusChanged>,
    IDomainEventHandler<ProjectMembersChanged>,
    IDomainEventHandler<ProjectStatusChanged>,
    IDomainEventHandler<CommentMentioned>,
    IDomainEventHandler<ApprovalStageOpened>,
    IDomainEventHandler<ApprovalDecided>,
    IDomainEventHandler<ApprovalSuperseded>,
    IDomainEventHandler<ApprovalReminded>,
    IDomainEventHandler<DelegationCreated>,
    IDomainEventHandler<PullRequestMerged>
{
    private static string ItemLink(Guid id) => $"/work-items/{id}";

    private static string ProjectLink(Guid id) => $"/projects/{id}";

    private static string ApprovalLink(Guid id) => $"/approvals/{id}";

    public Task Handle(WorkItemAssigneesChanged e, CancellationToken ct) => RunAsync(async (db, emit) =>
    {
        var body = $"{e.Ref} {e.Title}";
        await emit.EmitAsync(NotificationKinds.WorkItemAssigned, e.Added, "تم تكليفك بعمل", body, ItemLink(e.WorkItemId), e.ProjectId, "work-item", e.WorkItemId, ct);
        await emit.EmitAsync(NotificationKinds.WorkItemUnassigned, e.Removed, "أُزيل تكليفك من عمل", body, ItemLink(e.WorkItemId), e.ProjectId, "work-item", e.WorkItemId, ct);
    }, ct);

    public Task Handle(WorkItemStatusChanged e, CancellationToken ct) => RunAsync(async (db, emit) =>
    {
        var assignees = await db.WorkItems.AsNoTracking().Where(i => i.Id == e.WorkItemId).Select(i => i.AssigneeIds).SingleOrDefaultAsync(ct);
        var names = await StatusNamesAsync(db, e.FromStatusId, e.ToStatusId, ct);
        await emit.EmitAsync(
            NotificationKinds.WorkItemStatusChanged, assignees ?? [], "تغيّرت حالة عمل", $"{e.Ref}: {names[e.FromStatusId]} ← {names[e.ToStatusId]}",
            ItemLink(e.WorkItemId), e.ProjectId, "work-item", e.WorkItemId, ct);
    }, ct);

    public Task Handle(ProjectMembersChanged e, CancellationToken ct) => RunAsync(async (db, emit) =>
    {
        await emit.EmitAsync(NotificationKinds.ProjectMemberAdded, e.Added, "أُضفت إلى مشروع", e.Name, ProjectLink(e.ProjectId), e.ProjectId, "project", e.ProjectId, ct);
        await emit.EmitAsync(NotificationKinds.ProjectMemberRemoved, e.Removed, "أُزيلت من مشروع", e.Name, "/projects", null, "project", e.ProjectId, ct);
    }, ct);

    public Task Handle(ProjectStatusChanged e, CancellationToken ct) => RunAsync(async (db, emit) =>
    {
        var members = await db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == e.ProjectId).Select(m => m.UserId).ToListAsync(ct);
        var names = await StatusNamesAsync(db, e.FromStatusId, e.ToStatusId, ct);
        await emit.EmitAsync(
            NotificationKinds.ProjectStatusChanged, members, "تغيّرت حالة مشروع", $"{e.Name}: {names[e.FromStatusId]} ← {names[e.ToStatusId]}",
            ProjectLink(e.ProjectId), e.ProjectId, "project", e.ProjectId, ct);
    }, ct);

    public Task Handle(CommentMentioned e, CancellationToken ct) => RunAsync(async (db, emit) =>
    {
        var onItem = e.SubjectType == CommentSubjectType.WorkItem;
        var projectId = onItem
            ? await db.WorkItems.AsNoTracking().Where(i => i.Id == e.SubjectId).Select(i => (Guid?)i.ProjectId).SingleOrDefaultAsync(ct)
            : e.SubjectId;
        if (projectId is null)
        {
            return;
        }

        await emit.EmitAsync(
            NotificationKinds.CommentMentioned, e.Mentioned, "تمت الإشارة إليك في تعليق", e.Preview, onItem ? ItemLink(e.SubjectId) : ProjectLink(e.SubjectId),
            projectId, onItem ? "work-item" : "project", e.SubjectId, ct);
    }, ct);

    public Task Handle(ApprovalStageOpened e, CancellationToken ct) => RunAsync(async (db, emit) =>
    {
        var request = await db.ApprovalRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == e.RequestId, ct);
        if (request is null || request.CurrentStage == 0)
        {
            return;
        }

        var direct = request.DirectApproversOf(request.CurrentStage);
        var today = emit.Today;
        var delegates = await db.Delegations.AsNoTracking()
            .Where(d => d.IsActive && d.FromDate <= today && today <= d.ToDate && direct.Contains(d.FromUserId))
            .Select(d => d.ToUserId).ToListAsync(ct);
        await emit.EmitAsync(
            NotificationKinds.ApprovalRequested, direct.Concat(delegates).Where(u => u != request.RequesterId), "أُسند إليك طلب موافقة", request.Title,
            ApprovalLink(request.Id), request.ProjectId, "approval", request.Id, ct);
    }, ct);

    public Task Handle(ApprovalDecided e, CancellationToken ct) => RunAsync(async (db, emit) =>
    {
        var request = await db.ApprovalRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == e.RequestId, ct);
        if (request is null)
        {
            return;
        }

        var label = e.Decision switch { "Approve" => "موافقة", "Reject" => "رفض", _ => "إرجاع" };
        await emit.EmitAsync(
            NotificationKinds.ApprovalDecided, [request.RequesterId], $"قرار على طلبك: {label}", request.Title, ApprovalLink(request.Id), request.ProjectId, "approval", request.Id, ct);
    }, ct);

    public Task Handle(ApprovalSuperseded e, CancellationToken ct) => RunAsync(async (db, emit) =>
    {
        var request = await db.ApprovalRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == e.RequestId, ct);
        if (request is null)
        {
            return;
        }

        await emit.EmitAsync(
            NotificationKinds.ApprovalSuperseded, [request.RequesterId], "أُبطل طلب موافقة", $"{request.Title} — تغيّر حقل محمي، يلزم إعادة التقديم.",
            ApprovalLink(request.Id), request.ProjectId, "approval", request.Id, ct);
    }, ct);

    public Task Handle(ApprovalReminded e, CancellationToken ct) => RunAsync(async (db, emit) =>
    {
        var request = await db.ApprovalRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == e.RequestId, ct);
        if (request is null || request.CurrentStage == 0)
        {
            return;
        }

        var direct = request.DirectApproversOf(request.CurrentStage);
        var today = emit.Today;
        var delegates = await db.Delegations.AsNoTracking()
            .Where(d => d.IsActive && d.FromDate <= today && today <= d.ToDate && direct.Contains(d.FromUserId))
            .Select(d => d.ToUserId).ToListAsync(ct);
        await emit.EmitAsync(
            NotificationKinds.ApprovalReminded, direct.Concat(delegates), "تذكير بطلب موافقة بانتظارك", request.Title, ApprovalLink(request.Id), request.ProjectId, "approval", request.Id, ct);
    }, ct);

    public Task Handle(DelegationCreated e, CancellationToken ct) => RunAsync(async (db, emit) =>
    {
        var from = await db.Users.AsNoTracking().Where(u => u.Id == e.FromUserId).Select(u => u.Name).SingleOrDefaultAsync(ct) ?? string.Empty;
        await emit.EmitAsync(
            NotificationKinds.ApprovalDelegated, [e.ToUserId], "فُوّضت إليك صلاحية الموافقة", $"{from}: {e.From:yyyy-MM-dd} ← {e.To:yyyy-MM-dd}", "/approvals", null, "delegation", e.DelegationId, ct);
    }, ct);

    public Task Handle(PullRequestMerged e, CancellationToken ct) => RunAsync(async (db, emit) =>
    {
        var items = await db.WorkItems.AsNoTracking().Where(i => i.ProjectId == e.ProjectId && e.Refs.Contains(i.Ref)).Select(i => new { i.Id, i.Ref, i.AssigneeIds }).ToListAsync(ct);
        foreach (var item in items)
        {
            await emit.EmitAsync(
                NotificationKinds.GitPrMerged, item.AssigneeIds, "دُمج Pull Request مرتبط", $"{item.Ref}: {e.Title}", ItemLink(item.Id), e.ProjectId, "work-item", item.Id, ct);
        }
    }, ct);

    private static async Task<Dictionary<Guid, string>> StatusNamesAsync(IApplicationDbContext db, Guid from, Guid to, CancellationToken ct)
    {
        var names = await db.Statuses.AsNoTracking().Where(s => s.Id == from || s.Id == to).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        names.TryAdd(from, string.Empty);
        names.TryAdd(to, string.Empty);
        return names;
    }

    private async Task RunAsync(Func<IApplicationDbContext, NotificationEmitter, Task> body, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var emitter = scope.ServiceProvider.GetRequiredService<NotificationEmitter>();
        if (!emitter.InRequest)
        {
            return;
        }

        await body(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), emitter);
        await emitter.SaveAsync(cancellationToken);
    }
}
