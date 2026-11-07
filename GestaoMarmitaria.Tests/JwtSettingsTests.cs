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
                ExpiracaoEmHoras = 8
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
        public void Validar_ExpiracaoInvalida_DeveLancarExcecao(int horas)
        {
            var settings = CriarSettingsValidas();
            settings.ExpiracaoEmHoras = horas;

            var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

            Assert.Contains("Jwt:ExpiracaoEmHoras", excecao.Message);
        }
    }
}
