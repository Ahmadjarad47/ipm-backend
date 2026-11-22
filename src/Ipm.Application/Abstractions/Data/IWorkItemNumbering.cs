namespace Ipm.Application.Abstractions.Data;

/// <summary>
/// Hands out the next work item number of a project with one atomic statement (R1), so two requests at once never get the same
/// number. A number given and then not used (the save failed) is simply skipped.
/// </summary>
public interface IWorkItemNumbering
{
    Task<int> NextAsync(Guid projectId, CancellationToken cancellationToken);
}
