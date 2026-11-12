using GestaoMarmitaria.Api.Infra.Configurations;
using GestaoMarmitaria.Api.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestaoMarmitaria.Api.Infra.Services;

public class LimpezaRefreshTokensService : ILimpezaRefreshTokensService
{
    private readonly AppDbContext _context;
    private readonly LimpezaRefreshTokensSettings _settings;

    public LimpezaRefreshTokensService(
        AppDbContext context,
        IOptions<LimpezaRefreshTokensSettings> settings)
    {
        _context = context;
        _settings = settings.Value;
    }

    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var limite = DateTime.UtcNow.AddDays(-_settings.RetencaoEmDias);

        return await _context.RefreshTokens
            .Where(r => r.ExpiraEm < limite ||
                (r.RevogadoEm != null && r.RevogadoEm < limite))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
