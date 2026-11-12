using GestaoMarmitaria.Api.Infra.Configurations;
using Xunit;

namespace GestaoMarmitaria.Tests
{
    public class RateLimitingSettingsTests
    {
        [Fact]
        public void Validar_ConfiguracaoPadrao_NaoDeveLancarExcecao()
        {
            new RateLimitingSettings().Validar();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validar_LimiteInvalido_DeveLancarExcecao(int limite)
        {
            var settings = new RateLimitingSettings();
            settings.Login.LimiteTentativas = limite;

            var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

            Assert.Contains("RateLimiting:Login:LimiteTentativas", excecao.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validar_JanelaInvalida_DeveLancarExcecao(int segundos)
        {
            var settings = new RateLimitingSettings();
            settings.Login.JanelaEmSegundos = segundos;

            var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

            Assert.Contains("RateLimiting:Login:JanelaEmSegundos", excecao.Message);
        }
    }
}
