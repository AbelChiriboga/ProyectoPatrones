using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Pos.Application.Auth;

namespace Pos.Api.Security;

/// <summary>Adaptador de <see cref="ITokenIssuer"/> que firma un JWT con la clave configurada.</summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : ITokenIssuer
{
    public LoginResult Issue(string username)
    {
        var jwt = options.Value;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwt.ExpirationMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(new[] { new Claim("sub", username) }),
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new LoginResult(token, expiresAt, username);
    }
}
