namespace Ipm.Application.Features.Work.Comments.Common;

internal static class CommentPolicy
{
    /// <summary>R14: how long the author may edit a comment. A constant until the organization settings exist (part 09).</summary>
    public const int EditWindowHours = 24;
}
