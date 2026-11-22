namespace Ipm.Domain.Work;

/// <summary>
/// The last work item number given in a project. Numbers are handed out by one atomic statement (see <c>IWorkItemNumbering</c>),
/// never by <c>MAX(number) + 1</c>, so two requests at once never get the same number (R1).
/// </summary>
public sealed class ProjectCounter
{
    /// <summary>The first number of every project, so refs look like <c>ERP-101</c>.</summary>
    public const int FirstNumber = 101;

    private ProjectCounter() { }

    public Guid ProjectId { get; private init; }

    public int LastNumber { get; private set; }
}
