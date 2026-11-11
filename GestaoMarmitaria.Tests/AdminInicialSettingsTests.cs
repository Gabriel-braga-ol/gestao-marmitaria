using GestaoMarmitaria.Api.Configurations;
using Xunit;

namespace GestaoMarmitaria.Tests
{
    public class AdminInicialSettingsTests
    {
        private static AdminInicialSettings CriarSettingsValidas()
        {
            return new AdminInicialSettings
            {
                Nome = "Administrador",
                Email = "admin@marmitaria.com",
                Senha = "senhaSegura123"
            };
        }

        [Fact]
        public void Validar_SemConfiguracao_NaoDeveLancarExcecao()
        {
            var settings = new AdminInicialSettings();

            settings.Validar();
        }

        [Fact]
        public void Validar_ConfiguracaoValida_NaoDeveLancarExcecao()
        {
            var settings = CriarSettingsValidas();

            settings.Validar();
        }

        [Fact]
        public void Validar_ConfiguracaoParcial_DeveLancarExcecao()
        {
            var settings = new AdminInicialSettings
            {
                Email = "admin@marmitaria.com"
            };

            Assert.Throws<InvalidOperationException>(() => settings.Validar());
        }

        [Theory]
        [InlineData("")]
        [InlineData("email-invalido")]
        [InlineData("email@")]
        public void Validar_EmailInvalido_DeveLancarExcecao(string email)
        {
            var settings = CriarSettingsValidas();
            settings.Email = email;

            var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

            Assert.Contains("AdminInicial:Email", excecao.Message);
        }

        [Theory]
        [InlineData("123")]
        [InlineData("senha1234")]
        public void Validar_SenhaCurta_DeveLancarExcecao(string senha)
        {
            var settings = CriarSettingsValidas();
            settings.Senha = senha;

            var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

            Assert.Contains("AdminInicial:Senha", excecao.Message);
        }

        [Fact]
        public void Validar_SenhaLonga_DeveLancarExcecao()
        {
            var settings = CriarSettingsValidas();
            settings.Senha = new string('a', PoliticaSenha.TamanhoMaximo + 1);

            var excecao = Assert.Throws<InvalidOperationException>(() => settings.Validar());

            Assert.Contains("AdminInicial:Senha", excecao.Message);
        }

        [Theory]
        [InlineData(PoliticaSenha.TamanhoMinimo)]
        [InlineData(PoliticaSenha.TamanhoMaximo)]
        public void Validar_SenhaNosLimites_NaoDeveLancarExcecao(int tamanho)
        {
            var settings = CriarSettingsValidas();
            settings.Senha = new string('a', tamanho);

            settings.Validar();
        }
    }
}
