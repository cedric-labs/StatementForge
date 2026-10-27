using AwesomeAssertions;
using NetArchTest.Rules;
using StatementForge.Parsing;
using Xunit;

namespace StatementForge.Tests;

public class ArchitectureRulesTests
{
    [Fact]
    public void Interfaces_ShouldStartWithI()
    {
        var result = Types.InAssembly(typeof(IBankStatementParser).Assembly)
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Types_ShouldNotDependOn_MicrosoftAspNetCoreOrEntityFrameworkCore()
    {
        var result = Types.InAssembly(typeof(IBankStatementParser).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
