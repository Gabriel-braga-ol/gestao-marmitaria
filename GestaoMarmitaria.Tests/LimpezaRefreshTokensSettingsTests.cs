using GestaoMarmitaria.Api.Infra.Configurations;
using Xunit;

namespace GestaoMarmitaria.Tests;

public class LimpezaRefreshTokensSettingsTests
{
    [Fact]
    public void Validar_ConfiguracaoPadrao_NaoDeveLancarExcecao()
    {
        var settings = new LimpezaRefreshTokensSettings();

        settings.Validar();

        Assert.Equal(24, settings.IntervaloEmHoras);
        Assert.Equal(30, settings.RetencaoEmDias);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(LimpezaRefreshTokensSettings.IntervaloMaximoEmHoras + 1)]
    public void Validar_IntervaloForaDoLimite_DeveLancarExcecao(int horas)
    {
        var settings = new LimpezaRefreshTokensSettings { IntervaloEmHoras = horas };

        var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

        Assert.Contains("LimpezaRefreshTokens:IntervaloEmHoras", excecao.Message);
    }

    [Theory]
    [InlineData(LimpezaRefreshTokensSettings.IntervaloMinimoEmHoras)]
    [InlineData(LimpezaRefreshTokensSettings.IntervaloPadraoEmHoras)]
    [InlineData(LimpezaRefreshTokensSettings.IntervaloMaximoEmHoras)]
    public void Validar_IntervaloNoLimite_NaoDeveLancarExcecao(int horas)
    {
        var settings = new LimpezaRefreshTokensSettings { IntervaloEmHoras = horas };

        settings.Validar();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(LimpezaRefreshTokensSettings.RetencaoMaximaEmDias + 1)]
    public void Validar_RetencaoForaDoLimite_DeveLancarExcecao(int dias)
    {
        var settings = new LimpezaRefreshTokensSettings { RetencaoEmDias = dias };

        var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

        Assert.Contains("LimpezaRefreshTokens:RetencaoEmDias", excecao.Message);
    }

    [Theory]
    [InlineData(LimpezaRefreshTokensSettings.RetencaoMinimaEmDias)]
    [InlineData(LimpezaRefreshTokensSettings.RetencaoPadraoEmDias)]
    [InlineData(LimpezaRefreshTokensSettings.RetencaoMaximaEmDias)]
    public void Validar_RetencaoNoLimite_NaoDeveLancarExcecao(int dias)
    {
        var settings = new LimpezaRefreshTokensSettings { RetencaoEmDias = dias };

        settings.Validar();
    }
}
