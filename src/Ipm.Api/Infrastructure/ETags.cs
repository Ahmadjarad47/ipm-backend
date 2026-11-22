using System.Globalization;
using Ipm.Domain.Common;
using Microsoft.Net.Http.Headers;

namespace Ipm.Api.Infrastructure;

/// <summary>
/// Optimistic concurrency over HTTP. Every read of a single record sends <c>ETag: "&lt;version&gt;"</c>;
/// every update/delete must send it back in <c>If-Match</c>.
/// Missing header → 428, malformed → 400, stale → 412 (from the handler's <see cref="Error.StaleVersion"/>).
/// </summary>
public static class ETags
{
    public static string Format(uint version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>Adds the ETag header for the record being returned.</summary>
    public static void SetETag(this HttpResponse response, uint version) =>
        response.Headers.ETag = Format(version);

    /// <summary>
    /// Reads the version the client edited. Returns null and sets <paramref name="problem"/> when the header
    /// is missing or not one of our ETags, so the endpoint can return the problem as-is.
    /// </summary>
    public static uint? ReadIfMatch(this HttpRequest request, out IResult? problem)
    {
        problem = null;
        var header = request.Headers.IfMatch.ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            problem = Problem(StatusCodes.Status428PreconditionRequired, "Concurrency.IfMatchRequired",
                "أرسل رقم نسخة السجل في الترويسة If-Match (قيمة ETag التي وصلتك عند قراءة السجل).");
            return null;
        }

        if (EntityTagHeaderValue.TryParse(header, out var tag) && !tag.IsWeak
            && uint.TryParse(tag.Tag.AsSpan().Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            return version;
        }

        problem = Problem(StatusCodes.Status400BadRequest, "Concurrency.IfMatchInvalid",
            "قيمة If-Match غير صالحة. استخدم قيمة ETag كما وصلتك.");
        return null;
    }

    private static IResult Problem(int status, string code, string detail) =>
        Results.Problem(detail: detail, statusCode: status, extensions: new Dictionary<string, object?> { ["code"] = code });
}
