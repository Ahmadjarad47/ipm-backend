using Ipm.Application.Abstractions.Authentication;
using Ipm.Application.Abstractions.Data;
using Ipm.Application.Features.Projects.Projects.Common;
using Ipm.Domain.Common;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Ipm.Application.Features.Work.Comments.Common;

/// <summary>What a comment is on, resolved to the project that decides who may see and change it.</summary>
internal static class CommentSubjects
{
    public static bool TryParse(string? text, out CommentSubjectType type)
    {
        type = text == "project" ? CommentSubjectType.Project : CommentSubjectType.WorkItem;
        return text is "project" or "work-item";
    }

    /// <summary>The project of the subject, or null when the subject does not exist.</summary>
    public static async Task<Guid?> ProjectIdAsync(IApplicationDbContext db, CommentSubjectType type, Guid subjectId, CancellationToken cancellationToken)
    {
        if (type == CommentSubjectType.Project)
        {
            return await db.Projects.AsNoTracking().Where(p => p.Id == subjectId).Select(p => (Guid?)p.Id).SingleOrDefaultAsync(cancellationToken);
        }

        return await db.WorkItems.AsNoTracking().Where(i => i.Id == subjectId).Select(i => (Guid?)i.ProjectId).SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>The project of the subject after the access checks of <see cref="ProjectGuard"/>; a hidden or missing subject is the same 404.</summary>
    public static async Task<Result<GuardedProject>> GuardAsync(
        IApplicationDbContext db, IUserContext user, IAccessService access, ISecurityEventLog securityLog,
        CommentSubjectType type, Guid subjectId, string permission, bool track, CancellationToken cancellationToken)
    {
        var projectId = await ProjectIdAsync(db, type, subjectId, cancellationToken);
        if (projectId is null)
        {
            return CommentErrors.SubjectNotFound;
        }

        var guarded = track
            ? await ProjectGuard.ChangeAsync(db, user, access, securityLog, projectId.Value, permission, cancellationToken)
            : await ProjectGuard.ReadWithAsync(db, user, access, securityLog, projectId.Value, permission, cancellationToken);
        return guarded.IsFailure && guarded.Error == ProjectErrors.NotFound ? CommentErrors.SubjectNotFound : guarded;
    }

    /// <summary>R13: the @username mentions of <paramref name="text"/> that name an active member of the project.</summary>
    public static async Task<IReadOnlyList<Guid>> MentionsAsync(IApplicationDbContext db, Project project, string text, CancellationToken cancellationToken)
    {
        if (!text.Contains('@', StringComparison.Ordinal))
        {
            return [];
        }

        var memberIds = project.Members.Select(m => m.UserId).ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => memberIds.Contains(u.Id) && u.IsActive)
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync(cancellationToken);
        return CommentMentions.Extract(text, users.Select(u => (u.Id, u.UserName ?? string.Empty)));
    }
}
