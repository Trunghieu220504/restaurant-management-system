using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Restaurant.Modules.Identity.Application.Authentication;

namespace Restaurant.Api.Configuration;

public sealed class JwtAccessTokenIssuer(IOptions<JwtSettings> options) : IAccessTokenIssuer
{
    public string Issue(Guid userId, Guid sessionId, string[] roles, DateTime issuedAt, DateTime expiresAt)
    {
        var settings = options.Value;
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()), new("sid", sessionId.ToString()),
            new("jti", Guid.NewGuid().ToString()),
            new("iat", new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, issuedAt, expiresAt,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
