using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Regras
{
    public static class RegrasStatusPedido
    {
        public static bool PodeAlterarStatus(StatusPedido statusAtual, TipoEntrega tipoEntrega, StatusPedido novoStatus)
        {
            return statusAtual switch
            {
                StatusPedido.Novo => novoStatus == StatusPedido.EmPreparo,

                StatusPedido.EmPreparo => novoStatus == StatusPedido.Pronto,

                StatusPedido.Pronto => tipoEntrega == TipoEntrega.Entrega
                    ? novoStatus == StatusPedido.SaiuParaEntrega
                    : novoStatus == StatusPedido.Entregue,
                StatusPedido.SaiuParaEntrega => novoStatus == StatusPedido.Entregue,

                _ => false
            };
        }
    }
}
