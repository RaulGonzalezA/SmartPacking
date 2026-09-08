using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SmartPacking.Web;

public static class Auth0AccessTokenPermissions
{
    private static readonly string[] AdminPermissions = ["admin:users", "admin:plans", "admin:credits", "admin:billing", "admin:audit"];
    public static void AddTo(ClaimsIdentity identity, string? accessToken)
    {
        AddRolesAndPermissions(identity, identity.Claims.Where(claim => claim.Type is "roles" or "https://smartpacking.app/roles"));
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        JwtSecurityToken token;
        try
        {
            token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        }
        catch (ArgumentException)
        {
            return;
        }

        var permissions = token.Claims
            .Where(claim => claim.Type is "permissions" or "permission" or "scope" or "https://smartpacking.app/permissions")
            .SelectMany(claim => Auth0ClaimValues.Deserialize(claim.Value))
            .SelectMany(permission => permission == "admin:*" ? AdminPermissions : [permission])
            .Where(permission => permission.StartsWith("admin:", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal);

        foreach (var permission in permissions.Where(permission => !identity.HasClaim("permissions", permission)))
        {
            identity.AddClaim(new Claim("permissions", permission));
        }

        AddRolesAndPermissions(identity, token.Claims.Where(claim => claim.Type is "roles" or "https://smartpacking.app/roles"));
    }

    private static void AddRolesAndPermissions(ClaimsIdentity identity, IEnumerable<Claim> roleClaims)
    {
        var roles = roleClaims
            .SelectMany(claim => Auth0ClaimValues.Deserialize(claim.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var role in roles.Where(role => !identity.HasClaim(ClaimTypes.Role, role)))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

    }
}
