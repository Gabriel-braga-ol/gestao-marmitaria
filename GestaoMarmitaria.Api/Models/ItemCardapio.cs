namespace GestaoMarmitaria.Api.Models
{
    public class ItemCardapio
    {
        public int Id { get; set; }
        public DateOnly Data { get; set; }
        public int ProdutoId { get; set; }
        public bool Disponivel { get; set; } = true;

        public Produto Produto { get; set; } = null!;
    }
}