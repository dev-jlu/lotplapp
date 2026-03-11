using Lotplapp.Features.Users.Domain;
using Xunit;

namespace Lotplapp.Tests.Users;

/// <summary>
/// Unit tests for Features/Users/Domain/UserRoles.cs.
/// SPEC-1.1: The Reporter constant must exist and equal "Reporter".
/// SPEC-1.2: The class remains a constants-only class.
/// </summary>
public class UserRolesTests
{
    /// <summary>
    /// Task 1.1 — Verifies SPEC-1.1: UserRoles.Reporter constant exists and equals "Reporter".
    /// </summary>
    [Fact]
    public void Reporter_Constant_EqualsReporterString()
    {
        // Assert
        Assert.Equal("Reporter", UserRoles.Reporter);
    }

    /// <summary>
    /// Verifies all four role constants exist with the exact expected string values.
    /// </summary>
    [Theory]
    [InlineData("Admin", "Admin")]
    [InlineData("Owner", "Owner")]
    [InlineData("Seller", "Seller")]
    [InlineData("Reporter", "Reporter")]
    public void AllRoleConstants_HaveExpectedValues(string constantName, string expectedValue)
    {
        var type = typeof(UserRoles);
        var field = type.GetField(constantName);

        Assert.NotNull(field);
        Assert.Equal(expectedValue, field.GetValue(null));
    }
}
