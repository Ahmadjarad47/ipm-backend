using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ipm.IntegrationTests.Infrastructure;

namespace Ipm.IntegrationTests.Foundation;

/// <summary>Scenarios 4–6 of docs/modules/00-foundation.md: every error is Arabic problem+json with a traceId.</summary>
[Collection(ApiCollection.Name)]
public sealed class ErrorResponseTests(ApiFactory factory)
{
    [Fact]
    public async Task Unknown_route_is_404_problem()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative));
        var problem = await Problem.ReadAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("غير موجود", problem.Title);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
    }

    [Fact]
    public async Task Unexpected_exception_is_500_in_arabic_without_internals()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/_probe/boom", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();
        var problem = Problem.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("خطأ في الخادم", problem.Title);
        Assert.Equal("Server.Unexpected", problem.Code);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
        Assert.DoesNotContain("internal secret detail", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validation_failure_is_400_with_an_arabic_message_per_camel_case_field()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(new Uri("/api/v1/_probe/validate", UriKind.Relative), new { name = "" });
        var problem = await Problem.ReadAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("طلب غير صالح", problem.Title);
        Assert.Equal("Validation.Failed", problem.Code);

        var nameErrors = problem.Root.GetProperty("errors").GetProperty("name");
        var message = nameErrors[0].GetString()!;
        Assert.Contains(message, c => c is >= '؀' and <= 'ۿ');
    }

    [Fact]
    public async Task Malformed_json_body_is_400_in_arabic()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync(new Uri("/api/v1/_probe/validate", UriKind.Relative), content);
        var problem = await Problem.ReadAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("طلب غير صالح", problem.Title);
    }
}

/// <summary>Reads the parts of a problem+json body the tests check.</summary>
internal sealed record Problem(JsonElement Root)
{
    public string? Title => Root.TryGetProperty("title", out var v) ? v.GetString() : null;

    public string? Code => Root.TryGetProperty("code", out var v) ? v.GetString() : null;

    public string? TraceId => Root.TryGetProperty("traceId", out var v) ? v.GetString() : null;

    public static Problem Parse(string json) => new(JsonDocument.Parse(json).RootElement.Clone());

    public static async Task<Problem> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return Parse(await response.Content.ReadAsStringAsync());
    }
}
