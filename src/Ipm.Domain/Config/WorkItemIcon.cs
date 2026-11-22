namespace Ipm.Domain.Config;

/// <summary>The icons a work item type can use, the same names as the front end. Stored and shown as kebab-case text.</summary>
public enum WorkItemIcon
{
    Feature = 0,
    Task = 1,
    Bug = 2,
    Subtask = 3,
    Milestone = 4,
    Epic = 5,
    Story = 6,
}
