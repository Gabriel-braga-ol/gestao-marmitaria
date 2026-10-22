using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Dtos
{
    public class CriarItemPedidoDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "O ProdutoId deve ser maior que zero.")]
        public int ProdutoId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
        public int Quantidade { get; set; }
    }
}
