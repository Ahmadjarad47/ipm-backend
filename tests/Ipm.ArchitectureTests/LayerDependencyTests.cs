using System.Reflection;
using NetArchTest.Rules;

namespace Ipm.ArchitectureTests;

/// <summary>
/// Guards the Clean Architecture dependency rule: dependencies only point inward
/// (Api -> Infrastructure / RealTime -> Application -> Domain).
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(Domain.AssemblyReference).Assembly;
    private static readonly Assembly Application = typeof(Application.AssemblyReference).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.AssemblyReference).Assembly;

    private static readonly Assembly RealTime = typeof(RealTime.AssemblyReference).Assembly;

    private const string RealTimeNs = "Ipm.RealTime";
    private const string ApplicationNs = "Ipm.Application";
    private const string InfrastructureNs = "Ipm.Infrastructure";
    private const string ApiNs = "Ipm.Api";

    /// <summary>ASP.NET Core namespaces that mean HTTP.</summary>
    private static readonly string[] WebNamespaces =
    [
        "Microsoft.AspNetCore.Http",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Routing",
        "Microsoft.AspNetCore.Builder",
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Authentication",
        "Microsoft.AspNetCore.Authorization",
    ];

    [Fact]
    public void Domain_does_not_depend_on_any_other_layer_or_framework()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny([ApplicationNs, InfrastructureNs, ApiNs, "Mediator", "Microsoft.EntityFrameworkCore", .. WebNamespaces])
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Application_does_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny([InfrastructureNs, ApiNs, "Npgsql", .. WebNamespaces])
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn(ApiNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void RealTime_does_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(RealTime)
            .ShouldNot()
            .HaveDependencyOnAny([InfrastructureNs, ApiNs])
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Inner_layers_do_not_depend_on_RealTime()
    {
        foreach (var assembly in new[] { Domain, Application, Infrastructure })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn(RealTimeNs)
                .GetResult();

            Assert.True(result.IsSuccessful, $"{assembly.GetName().Name}: {Failures(result)}");
        }
    }

    [Fact]
    public void SignalR_lives_only_in_RealTime()
    {
        foreach (var assembly in new[] { Domain, Application, Infrastructure })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn("Microsoft.AspNetCore.SignalR")
                .GetResult();

            Assert.True(result.IsSuccessful, $"{assembly.GetName().Name}: {Failures(result)}");
        }
    }

    [Fact]
    public void Handlers_are_sealed_and_named_Handler()
    {
        var result = Types.InAssembly(Application)
            .That()
            .AreClasses().And().ImplementInterface(typeof(Mediator.ICommandHandler<,>))
            .Or()
            .AreClasses().And().ImplementInterface(typeof(Mediator.IQueryHandler<,>))
            .Should()
            .BeSealed()
            .And()
            .HaveNameEndingWith("Handler", StringComparison.Ordinal)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    private static string Failures(TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
