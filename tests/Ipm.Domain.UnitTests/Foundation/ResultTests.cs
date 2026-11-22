using Ipm.Domain.Common;

namespace Ipm.Domain.UnitTests.Foundation;

public sealed class ResultTests
{
    [Fact]
    public void Success_has_no_error_and_exposes_its_value()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_carries_its_error_and_hides_the_value()
    {
        Result<int> result = Error.NotFound("X.NotFound", "غير موجود");

        Assert.True(result.IsFailure);
        Assert.Equal("X.NotFound", result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_without_an_error_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.PreconditionFailed)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Failure)]
    public void Error_factories_set_the_matching_type(ErrorType type)
    {
        var error = type switch
        {
            ErrorType.Validation => Error.Validation("c", "d"),
            ErrorType.NotFound => Error.NotFound("c", "d"),
            ErrorType.Conflict => Error.Conflict("c", "d"),
            ErrorType.Forbidden => Error.Forbidden("c", "d"),
            ErrorType.PreconditionFailed => Error.PreconditionFailed("c", "d"),
            ErrorType.Unauthorized => Error.Unauthorized("c", "d"),
            _ => Error.Failure("c", "d"),
        };

        Assert.Equal(type, error.Type);
    }

    [Fact]
    public void Stale_version_is_a_precondition_failure()
    {
        Assert.Equal(ErrorType.PreconditionFailed, Error.StaleVersion.Type);
        Assert.Equal("Concurrency.StaleVersion", Error.StaleVersion.Code);
    }
}
