namespace Ipm.Domain.Config;

/// <summary>What a status or workflow applies to: a project, or a work item. Shown as kebab-case text (<c>work-item</c>).</summary>
public enum StatusScope
{
    Project = 0,
    WorkItem = 1,
}
