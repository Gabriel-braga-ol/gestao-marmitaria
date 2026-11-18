namespace GestaoMarmitaria.Api.Features.Cardapio
{
    public class ItemCardapioRespostaDto
    {
        public int Id { get; init; }
        public DateOnly Data { get; init; }
        public int ProdutoId { get; init; }
        public required string NomeProduto { get; init; }
        public decimal Preco { get; init; }
        public bool Disponivel { get; init; }
    }
}