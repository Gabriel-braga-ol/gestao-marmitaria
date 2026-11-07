using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GestaoMarmitaria.Api.Configurations;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GestaoMarmitaria.Api.Services;

public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;

    public TokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string GerarToken(string usuarioId, string email, string perfil)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var chave = Encoding.UTF8.GetBytes(_jwtSettings.ChaveSecreta);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuarioId),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, perfil)
            }),
            Expires = DateTime.UtcNow.AddHours(_jwtSettings.ExpiracaoEmHoras),
            Issuer = _jwtSettings.Emissor,
            Audience = _jwtSettings.Audiencia,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(chave),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}