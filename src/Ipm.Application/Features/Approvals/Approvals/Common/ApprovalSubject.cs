using Ipm.Domain.Approvals;
using Ipm.Domain.Projects;
using Ipm.Domain.Work;

namespace Ipm.Application.Features.Approvals.Approvals.Common;

/// <summary>A project or a work item an approval is about, loaded for reading (rules, snapshots, titles).</summary>
internal sealed record ApprovalSubject(ApprovalSubjectType Type, Guid Id, Project Project, WorkItem? Item)
{
    public Guid ProjectId => Project.Id;

    /// <summary>The label put in the request title: the project name, or the ref and title of the item.</summary>
    public string Label => Item is null ? Project.Name : $"{Item.Ref} {Item.Title}";
}
