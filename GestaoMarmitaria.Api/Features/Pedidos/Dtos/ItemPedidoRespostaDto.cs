namespace GestaoMarmitaria.Api.Features.Pedidos
{
    public class ItemPedidoRespostaDto
    {
        public int Id { get; init; }
        public int ProdutoId { get; init; }
        public required string NomeProduto { get; init; }
        public int Quantidade { get; init; }
        public decimal PrecoUnitario { get; init; }
        public decimal Subtotal => Quantidade * PrecoUnitario;
    }
}
