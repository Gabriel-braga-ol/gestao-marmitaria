namespace GestaoMarmitaria.Api.Dtos
{
    public class ItemCardapioRespostaDto
    {
        public int Id { get; set; }
        public DateOnly Data { get; set; }
        public int ProdutoId { get; set; }
        public required string NomeProduto { get; set; }
        public decimal Preco { get; set; }
        public bool Disponivel { get; set; }
    }
}