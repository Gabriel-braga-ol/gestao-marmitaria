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
    private readonly TimeProvider _timeProvider;

    public RefreshTokenService(
        IOptions<JwtSettings> jwtSettings,
        TimeProvider timeProvider)
    {
        _jwtSettings = jwtSettings.Value;
        _timeProvider = timeProvider;
    }

    public RefreshTokenGerado Gerar(int usuarioId, Guid familiaId)
    {
        var tokenEmTextoPuro = WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(TamanhoEmBytes));
        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        var registro = new RefreshToken
        {
            UsuarioId = usuarioId,
            FamiliaId = familiaId,
            Hash = CalcularHash(tokenEmTextoPuro),
            CriadoEm = agora,
            ExpiraEm = agora.AddDays(_jwtSettings.ExpiracaoRefreshEmDias)
        };

        return new RefreshTokenGerado(registro, tokenEmTextoPuro);
    }

    public string CalcularHash(string tokenEmTextoPuro)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(tokenEmTextoPuro));

        return Convert.ToHexString(bytes);
    }
}
