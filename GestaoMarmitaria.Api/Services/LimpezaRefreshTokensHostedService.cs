using GestaoMarmitaria.Api.Configurations;
using Microsoft.Extensions.Options;

namespace GestaoMarmitaria.Api.Services;

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
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha ao limpar refresh tokens; a rotina tentará novamente no próximo intervalo.");
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
}
