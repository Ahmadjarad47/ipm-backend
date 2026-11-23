namespace Ipm.Application.Features.Work.WorkItems.Common;

/// <summary>What Git shows about an item (part 07 R13): the commits that mention it and its open and merged pull requests.</summary>
public sealed record DevIndicator(int Commits, int OpenPrs, int MergedPrs);
