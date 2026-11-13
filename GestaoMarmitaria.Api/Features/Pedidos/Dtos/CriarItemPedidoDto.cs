using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Features.Pedidos
{
    public class CriarItemPedidoDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "O ProdutoId deve ser maior que zero.")]
        public int ProdutoId { get; set; }

        [Range(1, 100, ErrorMessage = "A quantidade deve estar entre 1 e 100.")]
        public int Quantidade { get; set; }
    }
}
