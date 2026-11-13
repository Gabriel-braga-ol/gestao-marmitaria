using GestaoMarmitaria.Api.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace GestaoMarmitaria.Tests
{
    public class FalhasBancoTests
    {
        private static PostgresException CriarPostgresException(
            string sqlState,
            string? constraintName = null)
        {
            return new PostgresException(
                "erro simulado",
                "ERROR",
                "ERROR",
                sqlState,
                constraintName: constraintName);
        }

        [Fact]
        public void EhFalhaDeSerializacao_PostgresException40001_DeveRetornarTrue()
        {
            var excecao = CriarPostgresException(PostgresErrorCodes.SerializationFailure);

            Assert.True(FalhasBanco.EhFalhaDeSerializacao(excecao));
        }

        [Fact]
        public void EhFalhaDeSerializacao_DbUpdateExceptionCom40001_DeveRetornarTrue()
        {
            var excecao = new DbUpdateException(
                "erro simulado",
                CriarPostgresException(PostgresErrorCodes.SerializationFailure));

            Assert.True(FalhasBanco.EhFalhaDeSerializacao(excecao));
        }

        [Fact]
        public void EhFalhaDeSerializacao_ExcecaoDeTransitoriedadeDoEf_DeveRetornarTrue()
        {
            var excecao = new InvalidOperationException(
                "An exception has been raised that is likely due to a transient failure.",
                new DbUpdateException(
                    "erro simulado",
                    CriarPostgresException(PostgresErrorCodes.SerializationFailure)));

            Assert.True(FalhasBanco.EhFalhaDeSerializacao(excecao));
        }

        [Fact]
        public void EhFalhaDeSerializacao_OutraFalha_DeveRetornarFalse()
        {
            Assert.False(FalhasBanco.EhFalhaDeSerializacao(
                new InvalidOperationException("erro simulado")));
        }

        [Fact]
        public void EhViolacaoDeIndiceUnico_IndiceEsperado_DeveRetornarTrue()
        {
            var excecao = new DbUpdateException(
                "erro simulado",
                CriarPostgresException(
                    PostgresErrorCodes.UniqueViolation,
                    "IX_Pagamentos_PedidoId"));

            Assert.True(FalhasBanco.EhViolacaoDeIndiceUnico(
                excecao,
                "IX_Pagamentos_PedidoId"));
        }

        [Fact]
        public void EhViolacaoDeIndiceUnico_OutroIndice_DeveRetornarFalse()
        {
            var excecao = new DbUpdateException(
                "erro simulado",
                CriarPostgresException(
                    PostgresErrorCodes.UniqueViolation,
                    "IX_Pagamentos_PedidoId"));

            Assert.False(FalhasBanco.EhViolacaoDeIndiceUnico(
                excecao,
                "IX_Reembolsos_PagamentoId"));
        }

        [Fact]
        public void EhViolacaoDeIndiceUnico_SqlStateDiferente_DeveRetornarFalse()
        {
            var excecao = new DbUpdateException(
                "erro simulado",
                CriarPostgresException(
                    PostgresErrorCodes.SerializationFailure,
                    "IX_Pagamentos_PedidoId"));

            Assert.False(FalhasBanco.EhViolacaoDeIndiceUnico(
                excecao,
                "IX_Pagamentos_PedidoId"));
        }
    }
}
