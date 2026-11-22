using Ipm.Domain.Work;

namespace Ipm.Domain.UnitTests.Work;

public sealed class CommentTests
{
    private static readonly Guid Author = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static Comment NewComment()
    {
        var comment = Comment.Create(CommentSubjectType.WorkItem, Guid.NewGuid(), Author, "  النص الأول ", [], Start);
        comment.SetCreated(Start, Author); // the save stamps this; here we set it
        return comment;
    }

    [Fact]
    public void R13_the_text_is_trimmed_and_is_the_first_revision()
    {
        var comment = NewComment();

        Assert.Equal("النص الأول", comment.Body);
        Assert.Equal("النص الأول", Assert.Single(comment.Revisions).Body);
        Assert.False(comment.IsEdited);
    }

    [Fact]
    public void R14_the_author_edits_inside_the_window_and_each_edit_adds_a_revision()
    {
        var comment = NewComment();

        Assert.True(comment.Edit(Author, "جديد", [], Start.AddHours(23), 24).IsSuccess);

        Assert.True(comment.IsEdited);
        Assert.Equal(2, comment.Revisions.Count);
        Assert.Equal("جديد", comment.Body);
    }

    [Fact]
    public void R14_another_user_the_expired_window_and_a_deleted_comment_are_refused()
    {
        var comment = NewComment();

        Assert.Equal("Comment.NotAuthor", comment.Edit(Guid.NewGuid(), "x", [], Start, 24).Error.Code);
        Assert.Equal("Comment.EditWindowExpired", comment.Edit(Author, "x", [], Start.AddHours(25), 24).Error.Code);

        Assert.True(comment.Delete().IsSuccess);
        Assert.Equal("Comment.Deleted", comment.Delete().Error.Code);
        Assert.Equal("Comment.Deleted", comment.Edit(Author, "x", [], Start, 24).Error.Code);
    }

    [Fact]
    public void R13_a_mention_starts_the_text_or_follows_a_space_and_ends_at_the_name()
    {
        var fahad = Guid.NewGuid();
        var reem = Guid.NewGuid();
        (Guid, string)[] users = [(fahad, "fahad"), (reem, "reem")];

        Assert.Equal([fahad], CommentMentions.Extract("@fahad شكراً", users));
        Assert.Equal([fahad, reem], CommentMentions.Extract("مرحباً @fahad و @reem", users));
        Assert.Empty(CommentMentions.Extract("mail@fahad.com و @fahadx", users));
    }
}
