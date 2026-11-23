using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Features.Work.WorkItems.Common;
using Ipm.Domain.Approvals;
using Ipm.Domain.Common;
using Ipm.Domain.Identity;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>06b: validating, checking and applying the proposed values of a sensitive change.</summary>
internal static class SensitiveChanges
{
    /// <summary>R2: protected fields that are only watched for supersession and can never be proposed.</summary>
    private static readonly string[] NotProposable = ["members", "relations"];
    private static readonly string[] ItemDates = ["startDate", "dueDate"];
    private static readonly string[] ProjectDates = ["startDate", "endDate"];

    public static bool IsProposable(ApprovalSubjectType type, string field) => ApprovalSubjectFields.For(type).Contains(field) && !NotProposable.Contains(field);

    /// <summary>R2: the proposed fields the template does not protect, or that cannot be proposed.</summary>
    public static List<string> NotAllowed(ApprovalTemplate template, ApprovalSubjectType type, IEnumerable<string> fields) =>
        [.. fields.Where(f => !template.ProtectedFields.Contains(f) || !IsProposable(type, f))];

    /// <summary>R3: the permission a direct edit of the field needs, which the requester must hold in the project.</summary>
    public static string PermissionFor(ApprovalSubjectType type, string field) =>
        type == ApprovalSubjectType.Project ? Permissions.ProjectsEdit
        : field == "assigneeIds" ? Permissions.TasksAssign
        : field == "typeId" ? Permissions.WorkItemTypesManage
        : Permissions.TasksEdit;

    public static string? MissingPermission(UserAccess access, Guid projectId, ApprovalSubjectType type, IEnumerable<string> fields) =>
        fields.Select(f => PermissionFor(type, f)).FirstOrDefault(p => !access.Can(p, projectId));

    /// <summary>R5: true when every proposed value equals the current one.</summary>
    public static async Task<bool> IsNoChangeAsync(IApplicationDbContext db, ApprovalSubject subject, IReadOnlyCollection<SnapshotEntry> proposed, CancellationToken cancellationToken)
    {
        var current = await ApprovalSubjects.SnapshotAsync(db, subject, proposed.Select(p => p.Field), cancellationToken);
        return proposed.All(p => Same(current.Find(c => c.Field == p.Field)?.Value, p.Value));
    }

    private static bool Same(string? a, string? b) => (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) || a == b;

    /// <summary>R4: every problem with the proposed values on the live data, with the path <c>proposedChanges.&lt;field&gt;</c>. Values are normalized first.</summary>
    public static async Task<(List<(string Path, string Message)> Problems, ProposedValues Values)> ValidateAsync(
        IApplicationDbContext db, ApprovalSubject subject, IReadOnlyCollection<SnapshotEntry> proposed, CancellationToken cancellationToken)
    {
        var raw = proposed.ToDictionary(p => p.Field, p => ProposedValues.Normalize(p.Field, p.Value));
        var values = new ProposedValues(raw);
        var problems = new List<(string, string)>();
        void Problem(string field, string message) => problems.Add(($"proposedChanges.{field}", message));

        if (subject.Item is { } item)
        {
            await ValidateItemAsync(db, subject, item, values, Problem, cancellationToken);
        }
        else
        {
            await ValidateProjectAsync(db, subject, values, Problem, cancellationToken);
        }

        return (problems, values);
    }

    private static async Task ValidateItemAsync(
        IApplicationDbContext db, ApprovalSubject subject, WorkItem item, ProposedValues values, Action<string, string> problem, CancellationToken cancellationToken)
    {
        if (values.Has("title") && (string.IsNullOrWhiteSpace(values.Text("title")) || values.Text("title")!.Length > WorkItem.TitleMaxLength))
        {
            problem("title", "العنوان مطلوب ولا يزيد على 200 حرف.");
        }

        if (values.Has("description") && (values.Text("description") ?? string.Empty).Length > WorkItem.DescriptionMaxLength)
        {
            problem("description", "الوصف طويل جداً.");
        }

        if (values.Has("priority") && !Enum.TryParse<Priority>(values.Text("priority"), ignoreCase: true, out _))
        {
            problem("priority", "أولوية غير صالحة.");
        }

        foreach (var field in ItemDates.Where(values.Has))
        {
            if (values.Text(field) is { } text && !ProposedValues.TryDate(text, out _))
            {
                problem(field, "تاريخ غير صالح (yyyy-MM-dd).");
            }
        }

        var start = values.Has("startDate") ? values.Date("startDate") : item.StartDate;
        var due = values.Has("dueDate") ? values.Date("dueDate") : item.DueDate;
        if (start is { } s && due is { } d && d < s)
        {
            problem("dueDate", "تاريخ الاستحقاق يسبق تاريخ البداية.");
        }

        var typeId = item.TypeId;
        if (values.Has("typeId"))
        {
            if (values.Id("typeId") is not { } wanted)
            {
                problem("typeId", "نوع غير صالح.");
            }
            else
            {
                typeId = wanted;
                foreach (var conflict in await TypeChangeAnalysis.ConflictsAsync(db, subject.Project, item, wanted, cancellationToken))
                {
                    problem("typeId", conflict.Reason);
                }
            }
        }

        if (values.Has("parentId") && values.Text("parentId") is not null)
        {
            if (values.Id("parentId") is not { } parentId)
            {
                problem("parentId", "أب غير صالح.");
            }
            else if (await WorkItemParents.EnsureAsync(db, subject.ProjectId, typeId, item.Id, parentId, cancellationToken) is { IsFailure: true } parent)
            {
                problem("parentId", parent.Error.Description);
            }
        }

        if (values.Has("assigneeIds"))
        {
            if (ProposedValues.ParseIds(values.Text("assigneeIds")) is not { } ids
                || (await WorkItemChecks.EnsureAssigneesAsync(db, subject.Project, ids, item.AssigneeIds, cancellationToken)).IsFailure)
            {
                problem("assigneeIds", "المسؤولون يجب أن يكونوا مستخدمين نشطين من أعضاء المشروع، بدون تكرار.");
            }
        }
    }

    private static async Task ValidateProjectAsync(
        IApplicationDbContext db, ApprovalSubject subject, ProposedValues values, Action<string, string> problem, CancellationToken cancellationToken)
    {
        var project = subject.Project;
        if (values.Has("name") && (string.IsNullOrWhiteSpace(values.Text("name")) || values.Text("name")!.Length > Project.NameMaxLength))
        {
            problem("name", "الاسم مطلوب ولا يزيد على 200 حرف.");
        }

        if (values.Has("description") && (values.Text("description") ?? string.Empty).Length > 4000)
        {
            problem("description", "الوصف طويل جداً.");
        }

        foreach (var field in ProjectDates.Where(values.Has))
        {
            if (!ProposedValues.TryDate(values.Text(field), out _))
            {
                problem(field, "تاريخ غير صالح (yyyy-MM-dd).");
            }
        }

        var start = values.Has("startDate") ? values.Date("startDate") : project.StartDate;
        var end = values.Has("endDate") ? values.Date("endDate") : project.EndDate;
        if (start is { } s && end is { } e && e < s)
        {
            problem("endDate", "تاريخ النهاية يسبق تاريخ البداية.");
        }

        if (values.Has("ownerId"))
        {
            var owner = values.Id("ownerId");
            var active = owner is { } id && await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.IsActive, cancellationToken);
            if (!active || !project.Members.Any(m => m.UserId == owner))
            {
                problem("ownerId", "يجب أن يكون المالك عضواً نشطاً بالمشروع.");
            }
        }
    }

    /// <summary>
    /// 06b R6: applies the approved values to the subject (all or nothing). Run before the final decision is recorded and saved in the same
    /// transaction. Nothing is changed unless every check passes.
    /// </summary>
    public static async Task<Result> ApplyAsync(
        IApplicationDbContext db, IAccessService access, ApprovalRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var project = await db.Projects.Include(p => p.Members).SingleAsync(p => p.Id == request.ProjectId, cancellationToken);
        var item = request.SubjectType == ApprovalSubjectType.WorkItem ? await db.WorkItems.SingleAsync(i => i.Id == request.SubjectId, cancellationToken) : null;
        if (project.IsArchived)
        {
            return ApprovalErrors.ProjectArchived;
        }

        if (item?.IsArchived == true)
        {
            return ApprovalErrors.SubjectArchived;
        }

        var subject = new ApprovalSubject(request.SubjectType, request.SubjectId, project, item);
        if ((await ApprovalSubjects.ChangedSinceAsync(db, subject, request.Snapshot, cancellationToken)).Count > 0)
        {
            return ApprovalErrors.ProtectedFieldsDrifted;
        }

        var (problems, values) = await ValidateAsync(db, subject, request.ProposedChanges, cancellationToken);
        if (problems.Count > 0)
        {
            return Error.Validation("Approval.ProposedChangesInvalid", string.Join(" ", problems.Select(p => p.Message).Distinct()));
        }

        var requester = await access.GetAsync(request.RequesterId, cancellationToken);
        var fields = request.ProposedChanges.Select(p => p.Field).ToList();
        if (requester is null || MissingPermission(requester, project.Id, request.SubjectType, fields) is not null)
        {
            return Error.Forbidden("Approval.RequesterNotAllowed", "مقدّم الطلب لم تعد له صلاحية تنفيذ هذا التعديل.");
        }

        var applied = item is null ? ApplyToProject(project, values) : ApplyToItem(item, values);
        if (applied.IsFailure)
        {
            return applied;
        }

        request.Consume(now);
        request.RefreshSnapshot(await ApprovalSubjects.SnapshotAsync(db, subject, request.Snapshot.Select(s => s.Field), cancellationToken));
        await ApprovalSupersession.SupersedeAsync(db, request.SubjectType, request.SubjectId, fields, cancellationToken, except: request.Id); // R7: the other pending requests that protect these fields
        return Result.Success();
    }

    private static Result ApplyToProject(Project project, ProposedValues values)
    {
        var updated = project.Update(
            values.Has("name") ? values.Text("name")! : project.Name,
            values.Has("description") ? values.Text("description") ?? string.Empty : project.Description,
            values.Has("startDate") ? values.Date("startDate")!.Value : project.StartDate,
            values.Has("endDate") ? values.Date("endDate")!.Value : project.EndDate);
        return updated.IsFailure || !values.Has("ownerId") ? updated : project.ChangeOwner(values.Id("ownerId")!.Value);
    }

    private static Result ApplyToItem(WorkItem item, ProposedValues values)
    {
        var updated = item.Update(
            values.Has("title") ? values.Text("title")! : item.Title,
            values.Has("description") ? values.Text("description") ?? string.Empty : item.Description,
            values.PriorityOr(item.Priority),
            values.Has("startDate") ? values.Date("startDate") : item.StartDate,
            values.Has("dueDate") ? values.Date("dueDate") : item.DueDate,
            values.Has("assigneeIds") ? values.Ids("assigneeIds") : item.AssigneeIds,
            item.Fields);
        if (updated.IsFailure)
        {
            return updated;
        }

        if (values.Has("parentId"))
        {
            item.SetParent(values.Id("parentId"));
        }

        if (values.Has("typeId"))
        {
            item.ChangeType(values.Id("typeId")!.Value, item.Fields);
        }

        return Result.Success();
    }
}
