using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ipm.IntegrationTests.Config;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Approvals;

/// <summary>Helpers that wrap the part 06 HTTP calls so the scenarios read like the spec.</summary>
internal static class ApprovalApi
{
    public static object Stage(int order, string name, string kind, string approver, int? dueHours = null) =>
        new { order, name, approverKind = kind, approverId = approver, dueHours };

    /// <summary>Two stages that the project manager (the administrator, in a project made by <see cref="NewProjectAsync"/>) decides: a position, then the user.</summary>
    public static object[] ManagerStages(Guid adminId, int? dueHours = null) =>
        [Stage(1, "مدير المشروع", "position", "project-manager", dueHours), Stage(2, "الاعتماد النهائي", "user", adminId.ToString())];

    public static async Task<Guid> CreateTemplateAsync(
        HttpClient admin, string approvalEvent, object[] stages, string[] protectedFields, object? rule = null, string? name = null, bool? active = null)
    {
        var response = await admin.PostAsJsonAsync(
            new Uri("/api/v1/approval-templates", UriKind.Relative),
            new { name = name ?? Unique("قالب"), @event = approvalEvent, active, stages, protectedFields, rule });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    public static async Task<(Guid Id, EntityTagHeaderValue ETag, JsonElement Body)> SubmitAsync(
        HttpClient client, Guid templateId, string subjectType, Guid subjectId, string? action = null)
    {
        var response = await client.PostAsJsonAsync(new Uri("/api/v1/approvals", UriKind.Relative), new { templateId, subjectType, subjectId, action });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        return (body.GetProperty("id").GetGuid(), response.Headers.ETag!, body);
    }

    public static Task<HttpResponseMessage> TrySubmitAsync(HttpClient client, Guid templateId, string subjectType, Guid subjectId, string? action = null) =>
        client.PostAsJsonAsync(new Uri("/api/v1/approvals", UriKind.Relative), new { templateId, subjectType, subjectId, action });

    public static Task<HttpResponseMessage> DecideAsync(HttpClient client, Guid id, string decision, string? comment, int revision, EntityTagHeaderValue? etag) =>
        SendAsync(client, HttpMethod.Post, $"/api/v1/approvals/{id}/decide", new { decision, comment, revision }, etag);

    public static Task<(JsonElement Body, EntityTagHeaderValue ETag)> GetApprovalAsync(HttpClient client, Guid id) => GetAsync(client, $"/api/v1/approvals/{id}");

    /// <summary>Approves every stage with the administrator (who decides each one), returning the last response.</summary>
    public static async Task<HttpResponseMessage> ApproveAllAsync(HttpClient admin, Guid id)
    {
        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++)
        {
            var (body, etag) = await GetApprovalAsync(admin, id);
            if (body.GetProperty("status").GetString() != "pending-approval")
            {
                break;
            }

            last = await DecideAsync(admin, id, "approve", null, body.GetProperty("revision").GetInt32(), etag);
            Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        }

        return last!;
    }

    /// <summary>
    /// A work item type whose workflow has one move that needs an approval, a project that allows it, one item in the first status,
    /// and a template governing closing it. Returns what a test needs.
    /// </summary>
    public static async Task<GatedItem> GatedItemAsync(HttpClient admin, object[]? stages = null)
    {
        var (a, _) = await WorkflowApi.CreateStatusAsync(admin, ConfigApi.NewKey("ga"));
        var (b, _) = await WorkflowApi.CreateStatusAsync(admin, ConfigApi.NewKey("gb"));
        var workflow = await admin.PostAsJsonAsync(
            new Uri("/api/v1/workflows", UriKind.Relative), WorkflowApi.WorkflowBody(ConfigApi.NewKey("gw"), [a, b], a, [WorkflowApi.Move(a, b, approval: true)]));
        Assert.Equal(HttpStatusCode.Created, workflow.StatusCode);
        var workflowId = (await ReadAsync(workflow)).GetProperty("id").GetGuid();

        var (typeId, typeEtag) = await ConfigApi.CreateTypeAsync(admin, ConfigApi.NewKey("gt"));
        Assert.Equal(
            HttpStatusCode.OK,
            (await SendAsync(admin, HttpMethod.Put, $"/api/v1/work-item-types/{typeId}", new { name = "نوع", color = "#112233", icon = "task", workflowId }, typeEtag)).StatusCode);

        var (project, _) = await NewProjectAsync(admin);
        var (_, projectEtag) = await GetProjectAsync(admin, project);
        Assert.Equal(
            HttpStatusCode.OK,
            (await SendAsync(admin, HttpMethod.Put, $"/api/v1/projects/{project}/work-item-types", new { allowedTypeIds = new[] { typeId }, defaultTypeId = typeId }, projectEtag)).StatusCode);

        var created = await admin.PostAsJsonAsync(
            new Uri("/api/v1/work-items", UriKind.Relative), new { projectId = project, typeId, title = "يحتاج موافقة", assigneeIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = (await ReadAsync(created)).GetProperty("id").GetGuid();

        var template = await CreateTemplateAsync(
            admin, "work-item-approve", stages ?? ManagerStages(await MyIdAsync(admin)), ["title", "dueDate"],
            new { workItemTypeIds = new[] { typeId }, actions = new[] { "close" } });
        return new GatedItem(project, typeId, item, b, template);
    }
}

internal sealed record GatedItem(Guid Project, Guid TypeId, Guid Item, Guid TargetStatus, Guid Template);
