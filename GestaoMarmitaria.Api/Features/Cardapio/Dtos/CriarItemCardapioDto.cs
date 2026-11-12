using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Features.Cardapio
{
    public class CriarItemCardapioDto
    {
        [Required(ErrorMessage = "A data do cardápio é obrigatória.")]
        public DateOnly? Data { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "O ID do produto deve ser maior que zero.")]
        public int ProdutoId { get; set; }
    }
}