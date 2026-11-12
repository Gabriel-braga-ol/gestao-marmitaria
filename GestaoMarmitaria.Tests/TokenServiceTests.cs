using System.IdentityModel.Tokens.Jwt;
using GestaoMarmitaria.Api.Configurations;
using GestaoMarmitaria.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestaoMarmitaria.Tests
{
    public class TokenServiceTests
    {
        private static TokenService CriarServico(int expiracaoEmHoras)
        {
            return new TokenService(Options.Create(new JwtSettings
            {
                ChaveSecreta = new string('a', JwtSettings.TamanhoMinimoChave),
                Emissor = "GestaoMarmitariaApi",
                Audiencia = "GestaoMarmitariaApp",
                ExpiracaoEmHoras = expiracaoEmHoras
            }));
        }

        [Theory]
        [InlineData(JwtSettings.ExpiracaoMinimaEmHoras)]
        [InlineData(JwtSettings.ExpiracaoPadraoEmHoras)]
        public void GerarToken_UsaAExpiracaoConfigurada(int horas)
        {
            var token = CriarServico(horas).GerarToken("7", "admin@marmitaria.com", "Admin");

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.Equal(
                TimeSpan.FromHours(horas),
                jwt.ValidTo - jwt.ValidFrom);
        }

        [Fact]
        public void GerarToken_IncluiIdentidade_Email_Perfil_Emissor_EAudiencia()
        {
            var token = CriarServico(JwtSettings.ExpiracaoPadraoEmHoras)
                .GerarToken("7", "admin@marmitaria.com", "Admin");

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.Equal("7", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.NameId).Value);
            Assert.Equal("admin@marmitaria.com", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
            Assert.Equal("Admin", jwt.Claims.Single(c => c.Type == "role").Value);
            Assert.Equal("GestaoMarmitariaApi", jwt.Issuer);
            Assert.Contains("GestaoMarmitariaApp", jwt.Audiences);
        }
    }
}
