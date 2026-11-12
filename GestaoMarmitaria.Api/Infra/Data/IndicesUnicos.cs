namespace GestaoMarmitaria.Api.Infra.Data;

public static class IndicesUnicos
{
    public const string PagamentoPorPedido = "IX_Pagamentos_PedidoId";
    public const string ReembolsoPorPagamento = "IX_Reembolsos_PagamentoId";
    public const string ItemCardapioPorDataEProduto = "IX_ItensCardapio_Data_ProdutoId";
    public const string UsuarioPorEmail = "IX_Usuarios_Email";
}
