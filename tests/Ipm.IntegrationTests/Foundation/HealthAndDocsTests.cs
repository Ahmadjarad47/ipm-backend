using System.Net;
using Ipm.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace Ipm.IntegrationTests.Foundation;

/// <summary>Scenarios 1–3 of docs/modules/00-foundation.md.</summary>
[Collection(ApiCollection.Name)]
public sealed class HealthAndDocsTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_is_200_when_postgres_and_redis_are_up()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_is_503_when_redis_is_unreachable()
    {
        using var redisDown = factory.WithWebHostBuilder(b =>
            b.UseSetting("ConnectionStrings:Cache", "localhost:1,abortConnect=false,connectTimeout=500"));
        using var client = redisDown.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("IPM API", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Docs_page_is_served()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/docs", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
