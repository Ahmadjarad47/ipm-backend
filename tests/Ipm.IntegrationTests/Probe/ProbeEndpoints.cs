using Ipm.Api.Endpoints;
using Ipm.Api.Infrastructure;
using Ipm.Application.Common.Exceptions;
using Ipm.Domain.Common;
using Ipm.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ValidationException = Ipm.Application.Common.Exceptions.ValidationException;

namespace Ipm.IntegrationTests.Probe;

public sealed record ProbeRequest(string Name);

public sealed record ProbeResponse(Guid Id, string Name, uint Version);

/// <summary>Arabic messages come from the global FluentValidation culture set in AddApplication.</summary>
public sealed class ProbeRequestValidator : AbstractValidator<ProbeRequest>
{
    public ProbeRequestValidator() => RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
}

/// <summary>
/// Test-only endpoints under <c>/api/v1/_probe</c>. The item endpoints follow the same ETag pattern
/// every real part uses: GET sends ETag, PUT/DELETE require If-Match and check it with HasVersion.
/// </summary>
public sealed class ProbeEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Open: these test the foundation mechanics, not authentication.
        var probe = app.MapGroup("_probe").AllowAnonymous();

        probe.MapGet("boom", IResult () => throw new InvalidOperationException("internal secret detail"));

        probe.MapPost("validate", (ProbeRequest request) =>
        {
            Validate(request);
            return Results.NoContent();
        });

        probe.MapPost("items", async (ProbeRequest request, ApplicationDbContext db, HttpResponse response) =>
        {
            Validate(request);
            var item = ProbeItem.Create(request.Name);
            db.Add(item);
            await db.SaveChangesAsync();

            response.SetETag(item.Version);
            return Results.Created($"/api/v1/_probe/items/{item.Id}", ToResponse(item));
        });

        probe.MapGet("items/{id:guid}", async (Guid id, ApplicationDbContext db, HttpResponse response) =>
        {
            var item = await db.Set<ProbeItem>().FindAsync(id);
            if (item is null)
            {
                return NotFound();
            }

            response.SetETag(item.Version);
            return Results.Ok(ToResponse(item));
        });

        probe.MapPut("items/{id:guid}", async (Guid id, ProbeRequest request, ApplicationDbContext db, HttpContext http) =>
        {
            if (http.Request.ReadIfMatch(out var problem) is not { } version)
            {
                return problem!;
            }

            var item = await db.Set<ProbeItem>().FindAsync(id);
            if (item is null)
            {
                return NotFound();
            }

            if (!item.HasVersion(version))
            {
                return Error.StaleVersion.ToProblem();
            }

            item.Rename(request.Name);
            await db.SaveChangesAsync();

            http.Response.SetETag(item.Version);
            return Results.Ok(ToResponse(item));
        });

        probe.MapDelete("items/{id:guid}", async (Guid id, ApplicationDbContext db, HttpRequest request) =>
        {
            if (request.ReadIfMatch(out var problem) is not { } version)
            {
                return problem!;
            }

            var item = await db.Set<ProbeItem>().FindAsync(id);
            if (item is null)
            {
                return NotFound();
            }

            if (!item.HasVersion(version))
            {
                return Error.StaleVersion.ToProblem();
            }

            db.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static void Validate(ProbeRequest request)
    {
        var result = new ProbeRequestValidator().Validate(request);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors
                .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                .ToDictionary(g => g.Key, g => g.ToArray()));
        }
    }

    private static IResult NotFound() => Error.NotFound("Probe.NotFound", "العمل غير موجود.").ToProblem();

    private static ProbeResponse ToResponse(ProbeItem item) => new(item.Id, item.Name, item.Version);
}
