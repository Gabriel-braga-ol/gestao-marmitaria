using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Dtos
{
    public class CriarProdutoDto
    {
        [Required(ErrorMessage = "O nome do produto é obrigatório.")]
        [StringLength(100, ErrorMessage = "O nome deve conter no máximo 100 caracteres.")]
        public required string Nome { get; set; }

        [StringLength(500, ErrorMessage = "A descrição deve conter no máximo 500 caracteres")]
        public string? Descricao { get; set; }

        [Range(typeof(decimal), "0.01", "9999.99", ParseLimitsInInvariantCulture = true, ErrorMessage = "O preço deve estar entre R$ 0,01 e R$ 9.999,99.")]
        public decimal Preco { get; set; }
    }
}
