using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace Mya.ArchitectureTests;

/// <summary>
/// The dependency rules from docs/02 section 1. Dependencies point inward only.
/// </summary>
public sealed class LayeringTests
{
    private static readonly Assembly DomainAssembly = typeof(Domain.AssemblyMarker).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;

    [Fact]
    public void Application_does_not_reference_EF_Core()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Application_does_not_reference_ASP_NET()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Domain_references_nothing_outside_the_BCL()
    {
        var foreign = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => !IsBcl(name))
            .ToList();

        foreign.ShouldBeEmpty("Mya.Domain must reference nothing, but references: " + string.Join(", ", foreign));
    }

    private static bool IsBcl(string assemblyName) =>
        assemblyName is "netstandard" or "mscorlib"
        || assemblyName.StartsWith("System", StringComparison.Ordinal);

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
