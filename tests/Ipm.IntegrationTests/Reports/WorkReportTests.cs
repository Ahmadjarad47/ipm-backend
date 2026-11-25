using System.Net;
using ClosedXML.Excel;
using Ipm.IntegrationTests.Infrastructure;
using static Ipm.IntegrationTests.Identity.IdentityApi;
using static Ipm.IntegrationTests.Projects.ProjectApi;
using static Ipm.IntegrationTests.Work.WorkApi;

namespace Ipm.IntegrationTests.Reports;

/// <summary>Spec 10: the work report (xlsx) of a period, for one project, several projects, or one person across projects.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkReportTests(ApiFactory factory)
{
    private const string ItemsSheet = "الأعمال";
    private const string SummarySheet = "الملخص";

    private static string Around => $"from={DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)):yyyy-MM-dd}&to={DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)):yyyy-MM-dd}";

    private static async Task<XLWorkbook> OpenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        return new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
    }

    private static List<string> Column(XLWorkbook book, string sheet, int column) =>
        [.. book.Worksheet(sheet).RowsUsed().Skip(1).Select(r => r.Cell(column).GetString())];

    private static Task<HttpResponseMessage> GetReportAsync(HttpClient client, string query) => client.GetAsync(new Uri($"/api/v1/reports/work?{query}", UriKind.Relative));

    [Fact]
    public async Task Scenario_1_a_project_report_lists_the_items_touched_in_the_period_with_a_summary_line()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        var (item, _, _) = await CreateAsync(admin, project, title: "عنصر التقرير");
        var ref1 = (await GetItemAsync(admin, item)).Body.GetProperty("ref").GetString()!;

        using var book = await OpenAsync(await GetReportAsync(admin, $"{Around}&projectIds={project}"));

        Assert.Equal([ref1], Column(book, ItemsSheet, 1));
        Assert.Contains("جديد", Column(book, ItemsSheet, 13)[0]);
        var summary = book.Worksheet(SummarySheet);
        Assert.Equal(1, summary.Cell(7, 3).GetValue<int>()); // touched
        Assert.Equal(1, summary.Cell(7, 4).GetValue<int>()); // created
        Assert.Equal(1, summary.Cell(7, 6).GetValue<int>()); // open now
    }

    [Fact]
    public async Task Scenario_2_a_period_without_activity_gives_an_empty_items_sheet()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        await CreateAsync(admin, project);

        using var book = await OpenAsync(await GetReportAsync(admin, $"from=2020-01-01&to=2020-01-31&projectIds={project}"));

        Assert.Empty(Column(book, ItemsSheet, 1));
        Assert.Equal(0, book.Worksheet(SummarySheet).Cell(7, 3).GetValue<int>());
    }

    [Fact]
    public async Task Scenario_3_several_projects_in_one_report_and_each_alone()
    {
        using var admin = await factory.AdminClientAsync();
        var (first, _) = await NewProjectAsync(admin);
        var (second, _) = await NewProjectAsync(admin);
        var (a, _, _) = await CreateAsync(admin, first, title: "في الأول");
        var (b, _, _) = await CreateAsync(admin, second, title: "في الثاني");
        var refA = (await GetItemAsync(admin, a)).Body.GetProperty("ref").GetString()!;
        var refB = (await GetItemAsync(admin, b)).Body.GetProperty("ref").GetString()!;

        using var both = await OpenAsync(await GetReportAsync(admin, $"{Around}&projectIds={first},{second}"));
        using var onlySecond = await OpenAsync(await GetReportAsync(admin, $"{Around}&projectIds={second}"));

        Assert.Equal(new[] { refA, refB }.Order(), Column(both, ItemsSheet, 1).Order());
        Assert.Equal(2, both.Worksheet(SummarySheet).RowsUsed().Count(r => r.RowNumber() >= 7 && r.Cell(3).GetString() == "1"));
        Assert.Equal([refB], Column(onlySecond, ItemsSheet, 1));
    }

    [Fact]
    public async Task Scenario_4_a_person_report_covers_only_the_items_assigned_to_them_across_projects()
    {
        using var admin = await factory.AdminClientAsync();
        var reem = await UserIdAsync(admin, "reem");
        var fahad = await UserIdAsync(admin, "fahad");
        var (first, _) = await NewProjectAsync(admin);
        var (second, _) = await NewProjectAsync(admin);
        var (mine1, _, _) = await CreateAsync(admin, first, assignees: [reem]);
        var (mine2, _, _) = await CreateAsync(admin, second, assignees: [reem, fahad]);
        var (other, _, _) = await CreateAsync(admin, first, assignees: [fahad]);
        var wanted = new[] { mine1, mine2 };
        var refs = new List<string>();
        foreach (var id in wanted)
        {
            refs.Add((await GetItemAsync(admin, id)).Body.GetProperty("ref").GetString()!);
        }

        var otherRef = (await GetItemAsync(admin, other)).Body.GetProperty("ref").GetString()!;

        using var book = await OpenAsync(await GetReportAsync(admin, $"{Around}&projectIds={first},{second}&assigneeId={reem}"));

        var listed = Column(book, ItemsSheet, 1);
        Assert.Equal(refs.Order(), listed.Order());
        Assert.DoesNotContain(otherRef, listed);
        Assert.Equal(await NameOfAsync(admin, reem), book.Worksheet(SummarySheet).Cell(3, 2).GetString());
    }

    [Fact]
    public async Task Scenario_5_a_title_that_looks_like_a_formula_stays_text()
    {
        using var admin = await factory.AdminClientAsync();
        var (project, _) = await NewProjectAsync(admin);
        await CreateAsync(admin, project, title: "=1+1");

        using var book = await OpenAsync(await GetReportAsync(admin, $"{Around}&projectIds={project}"));

        var cell = book.Worksheet(ItemsSheet).Cell(2, 3);
        Assert.False(cell.HasFormula);
        Assert.Equal("=1+1", cell.GetString()); // the leading apostrophe is the quote prefix: stored as text, shown as typed
        Assert.Equal(XLDataType.Text, cell.DataType);
    }

    [Fact]
    public async Task Scenario_6_a_member_without_the_export_permission_gets_nothing_and_a_hidden_project_is_not_revealed()
    {
        using var admin = await factory.AdminClientAsync();
        using var reem = await factory.SignedInClientAsync("reem", ApiFactory.DemoPassword);
        using var noura = await factory.SignedInClientAsync("noura", ApiFactory.DemoPassword);
        var (project, _) = await NewProjectAsync(admin);
        await CreateAsync(admin, project);

        using var memberBook = await OpenAsync(await GetReportAsync(reem, $"{Around}&projectIds={project}"));
        using var outsiderBook = await OpenAsync(await GetReportAsync(noura, $"{Around}&projectIds={project}"));

        Assert.Empty(Column(memberBook, ItemsSheet, 1));
        Assert.Empty(Column(outsiderBook, ItemsSheet, 1));
    }

    [Fact]
    public async Task Scenario_7_bad_input_is_refused_and_an_anonymous_caller_is_turned_away()
    {
        using var admin = await factory.AdminClientAsync();
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await GetReportAsync(admin, "from=2026-05-10&to=2026-05-01")).StatusCode);
        var tooLong = await GetReportAsync(admin, "from=2024-01-01&to=2026-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await GetReportAsync(admin, $"{Around}&projectIds=not-an-id")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetReportAsync(admin, $"{Around}&assigneeId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetReportAsync(anonymous, Around)).StatusCode);
    }

    private static async Task<string> NameOfAsync(HttpClient admin, Guid userId)
    {
        var body = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/users/{userId}", UriKind.Relative)));
        return body.GetProperty("name").GetString()!;
    }
}
