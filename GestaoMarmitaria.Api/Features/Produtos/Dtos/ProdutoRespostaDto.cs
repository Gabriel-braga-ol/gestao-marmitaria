using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Features.Produtos
{
    public class ProdutoRespostaDto
    {
        public int Id { get; set; }
        public required string Nome { get; set; }
        public string? Descricao { get; set; }
        public decimal Preco { get; set; }
        public TipoProduto Tipo { get; set; }
        public bool Ativo { get; set; }
    }
}
