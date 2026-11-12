namespace GestaoMarmitaria.Api.Infra.Services;

public interface ILimpezaRefreshTokensService
{
    Task<int> ExecutarAsync(CancellationToken cancellationToken = default);
}
