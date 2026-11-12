using GestaoMarmitaria.Api.Configurations;
using Xunit;

namespace GestaoMarmitaria.Tests
{
    public class JwtSettingsTests
    {
        private static JwtSettings CriarSettingsValidas()
        {
            return new JwtSettings
            {
                ChaveSecreta = new string('a', JwtSettings.TamanhoMinimoChave),
                Emissor = "GestaoMarmitariaApi",
                Audiencia = "GestaoMarmitariaApp",
                ExpiracaoEmMinutos = JwtSettings.ExpiracaoPadraoEmMinutos,
                ExpiracaoRefreshEmDias = JwtSettings.ExpiracaoRefreshPadraoEmDias
            };
        }

        [Fact]
        public void Validar_ConfiguracaoValida_NaoDeveLancarExcecao()
        {
            var settings = CriarSettingsValidas();

            settings.Validar();
        }

        [Theory]
        [InlineData("")]
        [InlineData("chave-curta")]
        public void Validar_ChaveAusenteOuCurta_DeveLancarExcecao(string chave)
        {
            var settings = CriarSettingsValidas();
            settings.ChaveSecreta = chave;

            var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

            Assert.Contains("Jwt:ChaveSecreta", excecao.Message);
        }

        [Fact]
        public void Validar_EmissorVazio_DeveLancarExcecao()
        {
            var settings = CriarSettingsValidas();
            settings.Emissor = "";

            var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

            Assert.Contains("Jwt:Emissor", excecao.Message);
        }

        [Fact]
        public void Validar_AudienciaVazia_DeveLancarExcecao()
        {
            var settings = CriarSettingsValidas();
            settings.Audiencia = "";

            var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

            Assert.Contains("Jwt:Audiencia", excecao.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(JwtSettings.ExpiracaoMaximaEmMinutos + 1)]
        public void Validar_ExpiracaoDoAccessTokenForaDoIntervalo_DeveLancarExcecao(int minutos)
        {
            var settings = CriarSettingsValidas();
            settings.ExpiracaoEmMinutos = minutos;

            var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

            Assert.Contains("Jwt:ExpiracaoEmMinutos", excecao.Message);
        }

        [Theory]
        [InlineData(JwtSettings.ExpiracaoMinimaEmMinutos)]
        [InlineData(JwtSettings.ExpiracaoPadraoEmMinutos)]
        [InlineData(JwtSettings.ExpiracaoMaximaEmMinutos)]
        public void Validar_ExpiracaoDoAccessTokenNoIntervalo_NaoDeveLancarExcecao(int minutos)
        {
            var settings = CriarSettingsValidas();
            settings.ExpiracaoEmMinutos = minutos;

            settings.Validar();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(JwtSettings.ExpiracaoRefreshMaximaEmDias + 1)]
        public void Validar_ExpiracaoDoRefreshForaDoIntervalo_DeveLancarExcecao(int dias)
        {
            var settings = CriarSettingsValidas();
            settings.ExpiracaoRefreshEmDias = dias;

            var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

            Assert.Contains("Jwt:ExpiracaoRefreshEmDias", excecao.Message);
        }

        [Theory]
        [InlineData(JwtSettings.ExpiracaoRefreshMinimaEmDias)]
        [InlineData(JwtSettings.ExpiracaoRefreshPadraoEmDias)]
        [InlineData(JwtSettings.ExpiracaoRefreshMaximaEmDias)]
        public void Validar_ExpiracaoDoRefreshNoIntervalo_NaoDeveLancarExcecao(int dias)
        {
            var settings = CriarSettingsValidas();
            settings.ExpiracaoRefreshEmDias = dias;

            settings.Validar();
        }

        [Fact]
        public void ExpiracaoPadrao_MantemAccessTokenCurto_ERefreshMaisLongo()
        {
            Assert.Equal(15, JwtSettings.ExpiracaoPadraoEmMinutos);
            Assert.Equal(7, JwtSettings.ExpiracaoRefreshPadraoEmDias);
            Assert.InRange(
                JwtSettings.ExpiracaoPadraoEmMinutos,
                JwtSettings.ExpiracaoMinimaEmMinutos,
                JwtSettings.ExpiracaoMaximaEmMinutos);
            Assert.InRange(
                JwtSettings.ExpiracaoRefreshPadraoEmDias,
                JwtSettings.ExpiracaoRefreshMinimaEmDias,
                JwtSettings.ExpiracaoRefreshMaximaEmDias);
        }
    }
}
