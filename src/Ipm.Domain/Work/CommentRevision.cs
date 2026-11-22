namespace Ipm.Domain.Work;

/// <summary>One version of a comment text. The first is the original (R14).</summary>
public sealed record CommentRevision(string Body, DateTimeOffset EditedAt);
