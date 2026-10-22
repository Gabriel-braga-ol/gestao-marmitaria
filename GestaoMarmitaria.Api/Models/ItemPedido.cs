namespace GestaoMarmitaria.Api.Models
{
    public class ItemPedido
    {
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public int ProdutoId { get; set; }
        public required string NomeProduto { get; set; }
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }

        public Pedido Pedido { get; set; } = null!;
        public Produto Produto { get; set; } = null!;
    }
}
