using GestaoMarmitaria.Api.Configurations;
using GestaoMarmitaria.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestaoMarmitaria.Tests;

public class RefreshTokenServiceTests
{
    private static RefreshTokenService CriarServico(int dias = 7)
    {
        return new RefreshTokenService(Options.Create(new JwtSettings
        {
            ChaveSecreta = new string('a', JwtSettings.TamanhoMinimoChave),
            Emissor = "GestaoMarmitariaApi",
            Audiencia = "GestaoMarmitariaApp",
            ExpiracaoEmMinutos = JwtSettings.ExpiracaoPadraoEmMinutos,
            ExpiracaoRefreshEmDias = dias
        }));
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
    public void CalcularHash_EhDeterministico()
    {
        var servico = CriarServico();
        const string valor = "token-de-teste";

        Assert.Equal(servico.CalcularHash(valor), servico.CalcularHash(valor));
        Assert.NotEqual(servico.CalcularHash(valor), servico.CalcularHash("outro-token"));
    }
}
