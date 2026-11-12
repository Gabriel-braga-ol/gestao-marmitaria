using GestaoMarmitaria.Api.Infra.Configurations;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Models;
using GestaoMarmitaria.Api.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestaoMarmitaria.Tests.IntegrationTests;

public class LimpezaRefreshTokensIntegrationTests : IntegracaoTesteBase
{
    public LimpezaRefreshTokensIntegrationTests(ApiIntegrationFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Executar_RemoveApenasTokensForaDaRetencao()
    {
        var usuarioId = await ObterUsuarioIdAsync(ApiIntegrationFixture.EmailAdmin);
        var familiaId = Guid.NewGuid();
        var agora = DateTime.UtcNow;

        await using var contexto = Fixture.CriarDbContext();

        await contexto.RefreshTokens.ExecuteDeleteAsync();

        contexto.RefreshTokens.AddRange(
            CriarToken(usuarioId, familiaId, "ativo", agora.AddDays(-1), agora.AddDays(30), null),
            CriarToken(usuarioId, familiaId, "expirado-antigo", agora.AddDays(-60), agora.AddDays(-31), null),
            CriarToken(usuarioId, familiaId, "expirado-recente", agora.AddDays(-10), agora.AddDays(20), null),
            CriarToken(usuarioId, familiaId, "revogado-antigo", agora.AddDays(-2), agora.AddDays(28), agora.AddDays(-31)),
            CriarToken(usuarioId, familiaId, "revogado-recente", agora.AddDays(-2), agora.AddDays(28), agora.AddDays(-10)),
            CriarToken(usuarioId, familiaId, "antigo-e-revogado", agora.AddDays(-40), agora.AddDays(-5), agora.AddDays(-35)));

        await contexto.SaveChangesAsync();

        var removidos = await CriarServico(contexto).ExecutarAsync();

        var restantes = await contexto.RefreshTokens
            .AsNoTracking()
            .Where(r => r.FamiliaId == familiaId)
            .OrderBy(r => r.Id)
            .Select(r => r.Hash)
            .ToListAsync();

        Assert.Equal(
            new[] { "ativo", "expirado-recente", "revogado-recente" },
            restantes);

        Assert.Equal(3, removidos);
    }

    [Fact]
    public async Task Executar_RespeitaRetencaoConfigurada()
    {
        var usuarioId = await ObterUsuarioIdAsync(ApiIntegrationFixture.EmailAdmin);
        var familiaId = Guid.NewGuid();
        var agora = DateTime.UtcNow;

        await using var contexto = Fixture.CriarDbContext();

        await contexto.RefreshTokens.ExecuteDeleteAsync();

        contexto.RefreshTokens.Add(CriarToken(
            usuarioId,
            familiaId,
            "expirado-ha-cinco-dias",
            agora.AddDays(-10),
            agora.AddDays(-5),
            null));

        await contexto.SaveChangesAsync();

        Assert.Equal(0, await CriarServico(contexto, retencaoEmDias: 7).ExecutarAsync());
        Assert.Equal(1, await CriarServico(contexto, retencaoEmDias: 3).ExecutarAsync());

        Assert.False(await contexto.RefreshTokens
            .AsNoTracking()
            .AnyAsync(r => r.FamiliaId == familiaId));
    }

    private static LimpezaRefreshTokensService CriarServico(
        AppDbContext contexto,
        int retencaoEmDias = 30)
    {
        return new LimpezaRefreshTokensService(
            contexto,
            Options.Create(new LimpezaRefreshTokensSettings
            {
                RetencaoEmDias = retencaoEmDias
            }));
    }

    private static RefreshToken CriarToken(
        int usuarioId,
        Guid familiaId,
        string hash,
        DateTime criadoEm,
        DateTime expiraEm,
        DateTime? revogadoEm)
    {
        return new RefreshToken
        {
            UsuarioId = usuarioId,
            FamiliaId = familiaId,
            Hash = hash,
            CriadoEm = criadoEm,
            ExpiraEm = expiraEm,
            RevogadoEm = revogadoEm
        };
    }
}
