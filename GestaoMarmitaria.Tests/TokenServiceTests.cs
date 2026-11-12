using System.IdentityModel.Tokens.Jwt;
using GestaoMarmitaria.Api.Configurations;
using GestaoMarmitaria.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace GestaoMarmitaria.Tests
{
    public class TokenServiceTests
    {
        private static TokenService CriarServico(int expiracaoEmMinutos)
        {
            return new TokenService(Options.Create(new JwtSettings
            {
                ChaveSecreta = new string('a', JwtSettings.TamanhoMinimoChave),
                Emissor = "GestaoMarmitariaApi",
                Audiencia = "GestaoMarmitariaApp",
                ExpiracaoEmMinutos = expiracaoEmMinutos
            }));
        }

        [Theory]
        [InlineData(JwtSettings.ExpiracaoMinimaEmMinutos)]
        [InlineData(JwtSettings.ExpiracaoPadraoEmMinutos)]
        public void GerarToken_UsaAExpiracaoConfigurada(int minutos)
        {
            var token = CriarServico(minutos)
                .GerarToken("7", "admin@marmitaria.com", "Admin");

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Valor);

            Assert.Equal(
                TimeSpan.FromMinutes(minutos),
                jwt.ValidTo - jwt.ValidFrom);
        }

        [Fact]
        public void GerarToken_IncluiIdentidade_Email_Perfil_Emissor_EAudiencia()
        {
            var token = CriarServico(JwtSettings.ExpiracaoPadraoEmMinutos)
                .GerarToken("7", "admin@marmitaria.com", "Admin");

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Valor);

            Assert.Equal("7", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.NameId).Value);
            Assert.Equal("admin@marmitaria.com", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
            Assert.Equal("Admin", jwt.Claims.Single(c => c.Type == "role").Value);
            Assert.False(string.IsNullOrWhiteSpace(jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value));
            Assert.Equal("GestaoMarmitariaApi", jwt.Issuer);
            Assert.Contains("GestaoMarmitariaApp", jwt.Audiences);
        }
    }
}
