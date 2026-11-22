using System.Diagnostics;

namespace Ipm.Api.Infrastructure;

/// <summary>
/// One place for the shape of every error response (RFC 9457 problem+json):
/// an Arabic title per status code and a <c>traceId</c> to find the request in Seq.
/// Applies to Result failures, validation errors, unhandled exceptions and empty status codes (404, 405…).
/// </summary>
public static class ProblemDetailsSetup
{
    private static readonly Dictionary<int, string> Titles = new()
    {
        [StatusCodes.Status400BadRequest] = "طلب غير صالح",
        [StatusCodes.Status401Unauthorized] = "يجب تسجيل الدخول",
        [StatusCodes.Status403Forbidden] = "غير مسموح",
        [StatusCodes.Status404NotFound] = "غير موجود",
        [StatusCodes.Status405MethodNotAllowed] = "العملية غير مدعومة على هذا الرابط",
        [StatusCodes.Status409Conflict] = "تعارض مع بيانات موجودة",
        [StatusCodes.Status412PreconditionFailed] = "تم تعديل السجل من مستخدم آخر",
        [StatusCodes.Status415UnsupportedMediaType] = "صيغة الطلب غير مدعومة",
        [StatusCodes.Status422UnprocessableEntity] = "مخالفة لقاعدة عمل",
        [StatusCodes.Status428PreconditionRequired] = "رقم نسخة السجل مطلوب",
        [StatusCodes.Status429TooManyRequests] = "طلبات كثيرة، حاول لاحقاً",
        [StatusCodes.Status500InternalServerError] = "خطأ في الخادم",
        [StatusCodes.Status503ServiceUnavailable] = "الخدمة غير متاحة حالياً",
    };

    public static IServiceCollection AddArabicProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            if (problem.Status is { } status && Titles.TryGetValue(status, out var title))
            {
                problem.Title = title;
            }

            problem.Type = null;
            problem.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        });
}
