using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartPacking.Api.DependencyInjection;
using Xunit;

namespace SmartPacking.Api.UnitTests;

public sealed class AuthenticationServiceCollectionExtensionsTests
{
    [Theory]
    [InlineData("AdminUsers", "admin:users")]
    [InlineData("AdminPlans", "admin:plans")]
    [InlineData("AdminCredits", "admin:credits")]
    [InlineData("AdminBilling", "admin:billing")]
    [InlineData("AdminAudit", "admin:audit")]
    public void PermissionPoliciesRequireThePermissionWithoutRequiringTheAdminRole(string policyName, string permission)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:Enabled"] = "false" })
            .Build();
        services.AddSmartPackingAuthentication(configuration);
        using var provider = services.BuildServiceProvider();
        var policy = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value.GetPolicy(policyName);

        policy.Should().NotBeNull();
        var claimRequirement = policy!.Requirements.OfType<ClaimsAuthorizationRequirement>().Single();
        claimRequirement.ClaimType.Should().Be("permissions");
        claimRequirement.AllowedValues.Should().Contain(permission);
        policy.Requirements.Should().NotContain(requirement => requirement is RolesAuthorizationRequirement);
    }
}
