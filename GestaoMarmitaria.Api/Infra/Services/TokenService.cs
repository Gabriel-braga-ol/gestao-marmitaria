using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GestaoMarmitaria.Api.Infra.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GestaoMarmitaria.Api.Infra.Services;

public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly TimeProvider _timeProvider;

    public TokenService(IOptions<JwtSettings> jwtSettings, TimeProvider timeProvider)
    {
        _jwtSettings = jwtSettings.Value;
        _timeProvider = timeProvider;
    }

    public TokenGerado GerarToken(string usuarioId, string email, string perfil)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var chave = Encoding.UTF8.GetBytes(_jwtSettings.ChaveSecreta);
        var expiraEm = _timeProvider.GetUtcNow().UtcDateTime
            .AddMinutes(_jwtSettings.ExpiracaoEmMinutos);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, usuarioId),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, perfil)
            }),
            Expires = expiraEm,
            Issuer = _jwtSettings.Emissor,
            Audience = _jwtSettings.Audiencia,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(chave),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return new TokenGerado(tokenHandler.WriteToken(token), expiraEm);
    }
}
