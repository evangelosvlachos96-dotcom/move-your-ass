using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace Mya.ArchitectureTests;

/// <summary>
/// The dependency rules from docs/02 section 1. Dependencies point inward only.
/// Mya.Application may reference Microsoft.EntityFrameworkCore (DbSet, async LINQ) but never
/// the SqlServer provider or ASP.NET. Mya.Domain references nothing.
/// </summary>
public sealed class LayeringTests
{
    private static readonly Assembly DomainAssembly = typeof(Domain.Constants.Roles).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;

    private static readonly string[] SqlServerProviderPrefixes =
    [
        "Microsoft.EntityFrameworkCore.SqlServer",
        "Microsoft.Data.SqlClient",
    ];

    [Fact]
    public void Application_does_not_reference_the_SqlServer_provider()
    {
        // The provider's extension methods live in the Microsoft.EntityFrameworkCore namespace
        // (e.g. UseSqlServer), so a namespace scan alone is not enough: check assembly references too.
        var referencedAssemblies = ReferencedAssembliesMatching(ApplicationAssembly, SqlServerProviderPrefixes);
        referencedAssemblies.ShouldBeEmpty(
            "Mya.Application must not reference the SqlServer provider, but references: " + string.Join(", ", referencedAssemblies));

        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(SqlServerProviderPrefixes)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Application_does_not_reference_ASP_NET()
    {
        var referencedAssemblies = ReferencedAssembliesMatching(ApplicationAssembly, ["Microsoft.AspNetCore"]);
        referencedAssemblies.ShouldBeEmpty(
            "Mya.Application must not reference ASP.NET, but references: " + string.Join(", ", referencedAssemblies));

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

    private static List<string> ReferencedAssembliesMatching(Assembly assembly, string[] prefixes) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => prefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

    private static bool IsBcl(string assemblyName) =>
        assemblyName is "netstandard" or "mscorlib"
        || assemblyName.StartsWith("System", StringComparison.Ordinal);

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
