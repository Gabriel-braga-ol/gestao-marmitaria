using GestaoMarmitaria.Api.Regras;
using Xunit;

namespace GestaoMarmitaria.Tests
{
    public class NormalizacaoEmailTests
    {
        [Theory]
        [InlineData("  Admin@Marmitaria.COM  ", "admin@marmitaria.com")]
        [InlineData("funcionario@marmitaria.test", "funcionario@marmitaria.test")]
        [InlineData("  USER@EXAMPLE.TEST", "user@example.test")]
        public void Normalizar_RemoveEspacosEReduzParaMinusculas(string entrada, string esperado)
        {
            Assert.Equal(esperado, NormalizacaoEmail.Normalizar(entrada));
        }

        [Fact]
        public void Normalizar_EIdempotente()
        {
            const string email = "admin@marmitaria.com";

            Assert.Equal(email, NormalizacaoEmail.Normalizar(NormalizacaoEmail.Normalizar(email)));
        }
    }
}
