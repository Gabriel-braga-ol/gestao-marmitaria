using System.Security.Cryptography;
using System.Text;
using GestaoMarmitaria.Api.Infra.Configurations;
using GestaoMarmitaria.Api.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace GestaoMarmitaria.Api.Infra.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private const int TamanhoEmBytes = 64;

    private readonly JwtSettings _jwtSettings;

    public RefreshTokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public RefreshTokenGerado Gerar(int usuarioId, Guid familiaId)
    {
        var valor = WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(TamanhoEmBytes));
        var agora = DateTime.UtcNow;

        var registro = new RefreshToken
        {
            UsuarioId = usuarioId,
            FamiliaId = familiaId,
            Hash = CalcularHash(valor),
            CriadoEm = agora,
            ExpiraEm = agora.AddDays(_jwtSettings.ExpiracaoRefreshEmDias)
        };

        return new RefreshTokenGerado(registro, valor);
    }

    public string CalcularHash(string valor)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(valor));

        return Convert.ToHexString(bytes);
    }
}
