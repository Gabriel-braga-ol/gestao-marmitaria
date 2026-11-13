using GestaoMarmitaria.Api.Infra.Configurations;
using GestaoMarmitaria.Api.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestaoMarmitaria.Api.Infra.Services;

public class LimpezaRefreshTokensService : ILimpezaRefreshTokensService
{
    private readonly AppDbContext _context;
    private readonly LimpezaRefreshTokensSettings _limpezaSettings;
    private readonly TimeProvider _timeProvider;

    public LimpezaRefreshTokensService(
        AppDbContext context,
        IOptions<LimpezaRefreshTokensSettings> limpezaSettings,
        TimeProvider timeProvider)
    {
        _context = context;
        _limpezaSettings = limpezaSettings.Value;
        _timeProvider = timeProvider;
    }

    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var limite = _timeProvider.GetUtcNow().UtcDateTime
            .AddDays(-_limpezaSettings.RetencaoEmDias);

        return await _context.RefreshTokens
            .Where(r => r.ExpiraEm < limite ||
                (r.RevogadoEm != null && r.RevogadoEm < limite))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
