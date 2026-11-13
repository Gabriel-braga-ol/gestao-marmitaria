using GestaoMarmitaria.Api.Models;

namespace GestaoMarmitaria.Api.Infra.Services;

public record RefreshTokenGerado(RefreshToken Registro, string Valor);

public interface IRefreshTokenService
{
    RefreshTokenGerado Gerar(int usuarioId, Guid familiaId);
    string CalcularHash(string valor);
}
