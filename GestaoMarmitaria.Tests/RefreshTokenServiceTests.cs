using GestaoMarmitaria.Api.Infra.Configurations;
using GestaoMarmitaria.Api.Infra.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace GestaoMarmitaria.Tests;

public class RefreshTokenServiceTests
{
    private static RefreshTokenService CriarServico(
        int dias = 7,
        TimeProvider? timeProvider = null)
    {
        return new RefreshTokenService(
            Options.Create(new JwtSettings
            {
                ChaveSecreta = new string('a', JwtSettings.TamanhoMinimoChave),
                Emissor = "GestaoMarmitariaApi",
                Audiencia = "GestaoMarmitariaApp",
                ExpiracaoEmMinutos = JwtSettings.ExpiracaoPadraoEmMinutos,
                ExpiracaoRefreshEmDias = dias
            }),
            timeProvider ?? TimeProvider.System);
    }

    [Fact]
    public void Gerar_ProduzTokenOpacoComHashEDatas()
    {
        var servico = CriarServico(dias: 7);
        var familiaId = Guid.NewGuid();

        var gerado = servico.Gerar(usuarioId: 3, familiaId);

        Assert.Equal(3, gerado.Registro.UsuarioId);
        Assert.Equal(familiaId, gerado.Registro.FamiliaId);
        Assert.False(string.IsNullOrWhiteSpace(gerado.Valor));
        Assert.DoesNotContain("=", gerado.Valor);
        Assert.Equal(64, gerado.Registro.Hash.Length);
        Assert.Equal(servico.CalcularHash(gerado.Valor), gerado.Registro.Hash);
        Assert.NotEqual(gerado.Valor, gerado.Registro.Hash);
        Assert.Equal(DateTimeKind.Utc, gerado.Registro.CriadoEm.Kind);
        Assert.Equal(DateTimeKind.Utc, gerado.Registro.ExpiraEm.Kind);
        Assert.Equal(
            TimeSpan.FromDays(7),
            gerado.Registro.ExpiraEm - gerado.Registro.CriadoEm);
        Assert.Null(gerado.Registro.RevogadoEm);
    }

    [Fact]
    public void Gerar_ProduzValoresDiferentesEmCadaChamada()
    {
        var servico = CriarServico();
        var familiaId = Guid.NewGuid();

        var primeiro = servico.Gerar(1, familiaId);
        var segundo = servico.Gerar(1, familiaId);

        Assert.NotEqual(primeiro.Valor, segundo.Valor);
        Assert.NotEqual(primeiro.Registro.Hash, segundo.Registro.Hash);
    }

    [Fact]
    public void Gerar_UsaRelogioInjetado()
    {
        var agora = new DateTimeOffset(2026, 11, 12, 15, 30, 0, TimeSpan.Zero);
        var servico = CriarServico(dias: 3, new FakeTimeProvider(agora));

        var gerado = servico.Gerar(usuarioId: 1, Guid.NewGuid());

        Assert.Equal(agora.UtcDateTime, gerado.Registro.CriadoEm);
        Assert.Equal(agora.UtcDateTime.AddDays(3), gerado.Registro.ExpiraEm);
    }

    [Fact]
    public void CalcularHash_EhDeterministico()
    {
        var servico = CriarServico();
        const string valor = "token-de-teste";

        Assert.Equal(servico.CalcularHash(valor), servico.CalcularHash(valor));
        Assert.NotEqual(servico.CalcularHash(valor), servico.CalcularHash("outro-token"));
    }
}
