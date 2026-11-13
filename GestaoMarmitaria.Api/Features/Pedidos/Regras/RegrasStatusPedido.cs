using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Features.Pedidos
{
    public static class RegrasStatusPedido
    {
        public static bool PodeAlterarStatus(StatusPedido statusAtual, TipoEntrega tipoEntrega, StatusPedido novoStatus)
        {
            return statusAtual switch
            {
                StatusPedido.Novo => novoStatus == StatusPedido.EmPreparo
                    || novoStatus == StatusPedido.Cancelado,

                StatusPedido.EmPreparo => novoStatus == StatusPedido.Pronto
                    || novoStatus == StatusPedido.Cancelado,

                StatusPedido.Pronto => tipoEntrega == TipoEntrega.Entrega
                    ? novoStatus == StatusPedido.SaiuParaEntrega
                    : novoStatus == StatusPedido.Entregue,
                StatusPedido.SaiuParaEntrega => novoStatus == StatusPedido.Entregue,

                _ => false
            };
        }
    }
}
