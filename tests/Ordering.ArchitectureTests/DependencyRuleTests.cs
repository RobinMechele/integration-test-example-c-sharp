using System.Reflection;
using NetArchTest.Rules;

namespace Ordering.ArchitectureTests;

/// <summary>
/// The Dependency Rule (Robert C. Martin, Clean Architecture, chapter 22): source code dependencies point only inwards.
/// Domain &lt;- Application &lt;- Infrastructure &lt;- Api
/// </summary>
public sealed class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(Ordering.Domain.Common.AggregateRoot).Assembly;
    private static readonly Assembly Application = typeof(Ordering.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Ordering.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(Ordering.Api.Endpoints.CustomerEndpoints).Assembly;

    [Fact]
    public void The_domain_only_references_the_base_class_library()
    {
        var references = Domain.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        references.ShouldAllBe(name => name == "netstandard" || name.StartsWith("System", StringComparison.Ordinal));
    }

    [Fact]
    public void The_domain_does_not_depend_on_any_other_layer_or_framework() =>
        ShouldHaveNoDependencies(
            Types.InAssembly(Domain),
            "Ordering.Application", "Ordering.Infrastructure", "Ordering.Api",
            "Microsoft.EntityFrameworkCore", "Oracle", "Mediator", "FluentValidation", "Microsoft.AspNetCore");

    [Fact]
    public void The_application_layer_does_not_depend_on_infrastructure_or_the_web() =>
        ShouldHaveNoDependencies(
            Types.InAssembly(Application),
            "Ordering.Infrastructure", "Ordering.Api",
            "Microsoft.EntityFrameworkCore", "Oracle", "Microsoft.AspNetCore");

    [Fact]
    public void The_infrastructure_layer_does_not_depend_on_the_web() =>
        ShouldHaveNoDependencies(Types.InAssembly(Infrastructure), "Ordering.Api", "Microsoft.AspNetCore");

    [Fact]
    public void Endpoints_only_talk_to_the_application_layer() =>
        ShouldHaveNoDependencies(
            Types.InAssembly(Api).That().ResideInNamespace("Ordering.Api.Endpoints"),
            "Ordering.Infrastructure", "Ordering.Domain", "Microsoft.EntityFrameworkCore");

    [Fact]
    public void Command_and_query_handlers_are_sealed()
    {
        var result = Types.InAssembly(Application)
            .That().HaveNameEndingWith("Handler")
            .Should().BeSealed()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    private static void ShouldHaveNoDependencies(PredicateList types, params string[] forbidden)
    {
        var result = types.ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    private static void ShouldHaveNoDependencies(Types types, params string[] forbidden)
    {
        var result = types.ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
