namespace GestaoMarmitaria.Api.Services;

public interface ILimpezaRefreshTokensService
{
    Task<int> ExecutarAsync(CancellationToken cancellationToken = default);
}
