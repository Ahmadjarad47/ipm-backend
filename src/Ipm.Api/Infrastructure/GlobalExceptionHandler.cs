using System.Text.Json;
using Ipm.Application.Common.Exceptions;
using Ipm.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ipm.Api.Infrastructure;

/// <summary>
/// Last line of defense: validation errors become 400 with an Arabic message per field (camelCase keys),
/// a lost concurrency race becomes 412, anything unexpected becomes a 500 without leaking internals.
/// Arabic titles and traceId are added centrally by <see cref="ProblemDetailsSetup"/>.
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException validation => new HttpValidationProblemDetails(
                validation.Errors.ToDictionary(e => JsonNamingPolicy.CamelCase.ConvertName(e.Key), e => e.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "بعض الحقول غير صحيحة.",
                Extensions = { ["code"] = "Validation.Failed" },
            },

            // Someone saved the same row between this request's read and its write (xmin changed).
            DbUpdateConcurrencyException => new ProblemDetails
            {
                Status = StatusCodes.Status412PreconditionFailed,
                Detail = Error.StaleVersion.Description,
                Extensions = { ["code"] = Error.StaleVersion.Code },
            },

            // Handlers check uniqueness first; this only catches two requests racing for the same code.
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Detail = "توجد قيمة مكررة لحقل يجب أن يكون فريداً (مثل الرمز). حدّث البيانات وأعد المحاولة.",
                Extensions = { ["code"] = "Conflict.Duplicate" },
            },

            BadHttpRequestException bad => new ProblemDetails
            {
                Status = bad.StatusCode,
                Detail = "تعذّرت قراءة الطلب. تأكد من صيغة البيانات المرسلة.",
                Extensions = { ["code"] = "Request.Malformed" },
            },

            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Detail = "حدث خطأ غير متوقع. حاول مرة أخرى.",
                Extensions = { ["code"] = "Server.Unexpected" },
            },
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
