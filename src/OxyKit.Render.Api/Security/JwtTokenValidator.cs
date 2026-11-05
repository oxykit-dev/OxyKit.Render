using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OxyKit.Render.Api.Security;

internal static class JwtTokenValidator
{
    public static bool TryValidate(
        string token,
        JwtOptions jwt,
        out ClaimsPrincipal? principal,
        out string? error
    )
    {
        principal = null;
        error = null;

        if (!jwt.IsConfigured)
        {
            error = "JWT is not configured.";
            return false;
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name,
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            principal = handler.ValidateToken(token, parameters, out _);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
