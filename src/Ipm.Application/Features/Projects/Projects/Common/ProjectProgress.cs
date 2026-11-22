namespace Ipm.Application.Features.Projects.Projects.Common;

/// <summary>
/// R13 of part 05: done leaf items over counted leaf items, as a whole percent, or null when there are none. <see cref="Rule"/> names
/// how it is computed, so a client can show it.
/// </summary>
public sealed record ProjectProgress(int? Percent, int DoneLeaves, int CountedLeaves, string Rule);
