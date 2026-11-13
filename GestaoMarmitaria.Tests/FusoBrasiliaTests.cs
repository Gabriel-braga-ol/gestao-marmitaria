using GestaoMarmitaria.Api.Infra.Regras;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace GestaoMarmitaria.Tests;

public class FusoBrasiliaTests
{
    [Fact]
    public void Hoje_UsaADataDeBrasilia_QuandoJaVirouODiaEmUtc()
    {
        // 13/11 às 02:00 UTC ainda é 12/11 às 23:00 em Brasília (UTC-3).
        var timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 11, 13, 2, 0, 0, TimeSpan.Zero));

        Assert.Equal(
            new DateOnly(2026, 11, 12),
            FusoBrasilia.Hoje(timeProvider));
    }

    [Fact]
    public void ParaUtc_ConverteOInicioDoDiaDeBrasilia()
    {
        var inicioUtc = FusoBrasilia.ParaUtc(new DateOnly(2026, 11, 12));

        Assert.Equal(
            new DateTime(2026, 11, 12, 3, 0, 0, DateTimeKind.Utc),
            inicioUtc);
    }
}
