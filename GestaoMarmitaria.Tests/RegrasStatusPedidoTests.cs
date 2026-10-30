using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Regras;
using Xunit;

namespace GestaoMarmitaria.Tests
{
    public class RegrasStatusPedidoTests
    {
        [Fact]
        public void PodeAlterarStatus_DeNovoParaEmPreparo_DeveRetornarTrue()
        {
            var statusAtual = StatusPedido.Novo;
            var tipoEntrega = TipoEntrega.Entrega;
            var novoStatus = StatusPedido.EmPreparo;

            var resultado = RegrasStatusPedido.PodeAlterarStatus(statusAtual, tipoEntrega, novoStatus);

            Assert.True(resultado);
        }

        [Fact]
        public void PodeAlterarStatus_DeNovoParaEntregue_DeveRetornarFalse()
        {
            var statusAtual = StatusPedido.Novo;
            var tipoEntrega = TipoEntrega.Entrega;
            var novoStatus = StatusPedido.Entregue;

            var resultado = RegrasStatusPedido.PodeAlterarStatus(statusAtual, tipoEntrega, novoStatus);

            Assert.False(resultado);
        }


        // verificam que uma retirada pode ser concluída quando estiver pronta e que não passa pela etapa de saída para entrega.
        [Fact]
        public void PodeAlterarStatus_RetiradaDeProntoParaEntregue_DeveRetornarTrue()
        {
            var statusAtual = StatusPedido.Pronto;
            var tipoEntrega = TipoEntrega.Retirada;
            var novoStatus = StatusPedido.Entregue;

            var resultado = RegrasStatusPedido.PodeAlterarStatus(statusAtual, tipoEntrega, novoStatus);

            Assert.True(resultado);
        }

        [Fact]
        public void PodeAlterarStatus_RetiradaDeProntoParaSaiuParaEntrega_DeveRetornarFalse()
        {
            var statusAtual = StatusPedido.Pronto;
            var tipoEntrega = TipoEntrega.Retirada;
            var novoStatus = StatusPedido.SaiuParaEntrega;

            var resultado = RegrasStatusPedido.PodeAlterarStatus(statusAtual, tipoEntrega, novoStatus);

            Assert.False(resultado);
        }


        // verifica que um pedido com entrega precisa passar por SaiuParaEntrega antes de ser concluído.
        [Fact]
        public void PodeAlterarStatus_EntregaDeProntoParaSaiuParaEntrega_DeveRetornarTrue()
        {
            var statusAtual = StatusPedido.Pronto;
            var tipoEntrega = TipoEntrega.Entrega;
            var novoStatus = StatusPedido.SaiuParaEntrega;

            var resultado = RegrasStatusPedido.PodeAlterarStatus(statusAtual, tipoEntrega, novoStatus);

            Assert.True(resultado);
        }

        [Fact]
        public void PodeAlterarStatus_EntregaDeProntoParaEntregue_DeveRetornarFalse()
        {
            var statusAtual = StatusPedido.Pronto;
            var tipoEntrega = TipoEntrega.Entrega;
            var novoStatus = StatusPedido.Entregue;

            var resultado = RegrasStatusPedido.PodeAlterarStatus(statusAtual, tipoEntrega, novoStatus);

            Assert.False(resultado);
        }

        // bloqueia o retorno do status; o segundo impede uma atualização que mantém o mesmo status.
        [Fact]
        public void PodeAlterarStatus_DeEmPreparoParaNovo_DeveRetornarFalse()
        {
            var statusAtual = StatusPedido.EmPreparo;
            var tipoEntrega = TipoEntrega.Entrega;
            var novoStatus = StatusPedido.Novo;

            var resultado = RegrasStatusPedido.PodeAlterarStatus(statusAtual, tipoEntrega, novoStatus);

            Assert.False(resultado);
        }

        [Fact]
        public void PodeAlterarStatus_DeEmPreparoParaEmPreparo_DeveRetornarFalse()
        {
            var statusAtual = StatusPedido.EmPreparo;
            var tipoEntrega = TipoEntrega.Entrega;
            var novoStatus = StatusPedido.EmPreparo;

            var resultado = RegrasStatusPedido.PodeAlterarStatus(statusAtual, tipoEntrega, novoStatus);

            Assert.False(resultado);
        }

        // verifica que um pedido Entregue não aceita nenhuma mudança de status.
        // Com [Theory] e [InlineData], o mesmo método roda uma vez para cada conjunto de valores:
        [Theory]
        [InlineData(StatusPedido.Novo)]
        [InlineData(StatusPedido.EmPreparo)]
        [InlineData(StatusPedido.Pronto)]
        [InlineData(StatusPedido.SaiuParaEntrega)]
        [InlineData(StatusPedido.Entregue)]
        public void PodeAlterarStatus_PedidoEntregue_DeveRetornarFalse(StatusPedido novoStatus)
        {
            var resultado = RegrasStatusPedido.PodeAlterarStatus(
                StatusPedido.Entregue,
                TipoEntrega.Entrega,
                novoStatus);

            Assert.False(resultado);
        }

        [Theory]
        [InlineData(StatusPedido.EmPreparo, StatusPedido.Pronto)]
        [InlineData(StatusPedido.SaiuParaEntrega, StatusPedido.Entregue)]
        public void PodeAlterarStatus_EntregaComTransicaoPermitida_DeveRetornarTrue(StatusPedido statusAtual,StatusPedido novoStatus)
        {
            var resultado = RegrasStatusPedido.PodeAlterarStatus(
                statusAtual,
                TipoEntrega.Entrega,
                novoStatus);

            Assert.True(resultado);
        }
    }
}
