using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Features.Produtos
{
    public class ProdutoRespostaDto
    {
        public int Id { get; init; }
        public required string Nome { get; init; }
        public string? Descricao { get; init; }
        public decimal Preco { get; init; }
        public TipoProduto Tipo { get; init; }
        public bool Ativo { get; init; }
    }
}
