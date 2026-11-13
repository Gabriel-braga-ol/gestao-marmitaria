using GestaoMarmitaria.Api.Infra.Configurations;
using Microsoft.Extensions.Options;

namespace GestaoMarmitaria.Api.Infra.Services;

public class LimpezaRefreshTokensHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<LimpezaRefreshTokensSettings> _settings;
    private readonly ILogger<LimpezaRefreshTokensHostedService> _logger;

    public LimpezaRefreshTokensHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<LimpezaRefreshTokensSettings> settings,
        ILogger<LimpezaRefreshTokensHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!await ExecutarLimpezaAsync(stoppingToken))
            {
                break;
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromHours(_settings.Value.IntervaloEmHoras),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Executa uma rodada. Retorna false quando o encerramento foi solicitado;
    /// falhas são registradas e a rodada é repetida no próximo intervalo.
    /// </summary>
    private async Task<bool> ExecutarLimpezaAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var servico = scope.ServiceProvider
                .GetRequiredService<ILimpezaRefreshTokensService>();

            var removidos = await servico.ExecutarAsync(stoppingToken);

            if (removidos > 0)
            {
                _logger.LogInformation(
                    "Limpeza de refresh tokens removeu {Quantidade} registro(s) fora da retenção.",
                    removidos);
            }

            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao limpar refresh tokens; a rotina tentará novamente no próximo intervalo.");

            return true;
        }
    }
}
