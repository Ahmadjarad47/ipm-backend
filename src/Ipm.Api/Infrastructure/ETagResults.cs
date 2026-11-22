using Ipm.Domain.Common;

namespace Ipm.Api.Infrastructure;

/// <summary>Single-record responses: the body plus <c>ETag</c> (the record's version) on success.</summary>
public static class ETagResults
{
    public static IResult ToETagResult<T>(this Result<T> result, HttpResponse response, Func<T, uint> version)
    {
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        response.SetETag(version(result.Value));
        return Results.Ok(result.Value);
    }

    public static IResult ToCreatedETagResult<T>(this Result<T> result, HttpResponse response, Func<T, uint> version, Func<T, string> location)
    {
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        response.SetETag(version(result.Value));
        return Results.Created(location(result.Value), result.Value);
    }
}
