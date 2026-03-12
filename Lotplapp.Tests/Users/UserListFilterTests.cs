using System.Collections.Generic;
using System.Linq;
using Lotplapp.Features.Users.Domain;
using Xunit;

namespace Lotplapp.Tests.Users;

/// <summary>
/// Unit tests for the FilteredUsers computed property in UserList.razor.cs.
/// A lightweight harness replicates the filter state and LINQ query so no
/// Blazor DI pipeline is needed.
/// </summary>
public class UserListFilterTests
{
    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void FilteredUsers_NoFilter_ReturnsAllUsers()
    {
        // Arrange
        var harness = new UserListFilterHarness();
        var users = new List<User>
        {
            new() { Id = "1", FullName = "Alice Admin",  Email = "alice@test.com", IsActive = true },
            new() { Id = "2", FullName = "Bob Seller",   Email = "bob@test.com",   IsActive = true },
            new() { Id = "3", FullName = "Carol Owner",  Email = "carol@test.com", IsActive = false },
        };
        var roles = new Dictionary<string, string> { ["1"] = "Admin", ["2"] = "Seller", ["3"] = "Owner" };
        harness.SetUsers(users, roles);
        harness.SetFilters(); // no filters

        // Act
        var result = harness.FilteredUsers.ToList();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void FilteredUsers_NameFilter_MatchesFullNameCaseInsensitive()
    {
        // Arrange
        var harness = new UserListFilterHarness();
        var users = new List<User>
        {
            new() { Id = "1", FullName = "Alice Admin", Email = "alice@test.com", IsActive = true },
            new() { Id = "2", FullName = "Bob Seller",  Email = "bob@test.com",   IsActive = true },
        };
        harness.SetUsers(users, new Dictionary<string, string> { ["1"] = "Admin", ["2"] = "Seller" });
        harness.SetFilters(name: "alice");

        // Act
        var result = harness.FilteredUsers.ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Alice Admin", result[0].FullName);
    }

    [Fact]
    public void FilteredUsers_NameFilter_MatchesEmailCaseInsensitive()
    {
        // Arrange
        var harness = new UserListFilterHarness();
        var users = new List<User>
        {
            new() { Id = "1", FullName = "Bob Seller", Email = "bob@test.com", IsActive = true },
        };
        harness.SetUsers(users, new Dictionary<string, string> { ["1"] = "Seller" });
        harness.SetFilters(name: "BOB");

        // Act
        var result = harness.FilteredUsers.ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("bob@test.com", result[0].Email);
    }

    [Fact]
    public void FilteredUsers_RoleFilter_ReturnsOnlyMatchingRole()
    {
        // Arrange
        var harness = new UserListFilterHarness();
        var users = new List<User>
        {
            new() { Id = "1", FullName = "Alice Admin", Email = "alice@test.com", IsActive = true },
            new() { Id = "2", FullName = "Bob Seller",  Email = "bob@test.com",   IsActive = true },
        };
        harness.SetUsers(users, new Dictionary<string, string> { ["1"] = "Admin", ["2"] = "Seller" });
        harness.SetFilters(role: "Admin");

        // Act
        var result = harness.FilteredUsers.ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("1", result[0].Id);
    }

    [Fact]
    public void FilteredUsers_StatusFilter_Inactive_ReturnsOnlyInactiveUsers()
    {
        // Arrange
        var harness = new UserListFilterHarness();
        var users = new List<User>
        {
            new() { Id = "1", FullName = "Active User",   Email = "active@test.com",   IsActive = true  },
            new() { Id = "2", FullName = "Inactive User", Email = "inactive@test.com", IsActive = false },
        };
        harness.SetUsers(users, new Dictionary<string, string> { ["1"] = "Seller", ["2"] = "Seller" });
        harness.SetFilters(status: "inactive");

        // Act
        var result = harness.FilteredUsers.ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Inactive User", result[0].FullName);
    }

    [Fact]
    public void FilteredUsers_CombinedFilter_RoleAndStatus()
    {
        // Arrange
        var harness = new UserListFilterHarness();
        var users = new List<User>
        {
            new() { Id = "1", FullName = "Active Admin",   Email = "a1@test.com", IsActive = true  },
            new() { Id = "2", FullName = "Inactive Admin", Email = "a2@test.com", IsActive = false },
            new() { Id = "3", FullName = "Active Seller",  Email = "s1@test.com", IsActive = true  },
            new() { Id = "4", FullName = "Inactive Seller",Email = "s2@test.com", IsActive = false },
        };
        harness.SetUsers(users, new Dictionary<string, string>
        {
            ["1"] = "Admin", ["2"] = "Admin", ["3"] = "Seller", ["4"] = "Seller"
        });
        harness.SetFilters(role: "Admin", status: "active");

        // Act
        var result = harness.FilteredUsers.ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Active Admin", result[0].FullName);
    }

    [Fact]
    public void FilteredUsers_NameFilter_NoMatch_ReturnsEmpty()
    {
        // Arrange
        var harness = new UserListFilterHarness();
        var users = new List<User>
        {
            new() { Id = "1", FullName = "Alice", Email = "alice@test.com", IsActive = true },
        };
        harness.SetUsers(users, new Dictionary<string, string> { ["1"] = "Admin" });
        harness.SetFilters(name: "zzznomatch");

        // Act
        var result = harness.FilteredUsers.ToList();

        // Assert
        Assert.Empty(result);
    }
}

/// <summary>
/// Lightweight harness that replicates the filter fields and FilteredUsers LINQ
/// from UserList.razor.cs without requiring Blazor DI or rendering.
/// </summary>
public class UserListFilterHarness
{
    private List<User>? _users;
    private Dictionary<string, string> _roles = [];

    private string _nameFilter = string.Empty;
    private string _roleFilter = string.Empty;
    private string _statusFilter = string.Empty;

    public IEnumerable<User> FilteredUsers =>
        (_users ?? [])
            .Where(u => string.IsNullOrEmpty(_nameFilter) ||
                        u.FullName.Contains(_nameFilter, StringComparison.OrdinalIgnoreCase) ||
                        (u.Email ?? "").Contains(_nameFilter, StringComparison.OrdinalIgnoreCase))
            .Where(u => string.IsNullOrEmpty(_roleFilter) ||
                        _roles.GetValueOrDefault(u.Id) == _roleFilter)
            .Where(u => string.IsNullOrEmpty(_statusFilter) ||
                        (_statusFilter == "active" && u.IsActive) ||
                        (_statusFilter == "inactive" && !u.IsActive));

    public void SetUsers(List<User> users, Dictionary<string, string> roles)
    {
        _users = users;
        _roles = roles;
    }

    public void SetFilters(string name = "", string role = "", string status = "")
    {
        _nameFilter = name;
        _roleFilter = role;
        _statusFilter = status;
    }
}
