namespace GestaoMarmitaria.Api.Dtos
{
    public class ItemPedidoRespostaDto
    {
        public int Id { get; set; }
        public int ProdutoId { get; set; }
        public required string NomeProduto { get; set; }
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }
        public decimal Subtotal => Quantidade * PrecoUnitario;
    }
}
