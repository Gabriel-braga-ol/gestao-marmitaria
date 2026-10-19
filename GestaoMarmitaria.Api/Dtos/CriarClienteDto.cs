using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Dtos
{
    public class CriarClienteDto
    {
        [Required(ErrorMessage = "O nome do cliente é obrigatório.")]
        [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
        public required string Nome { get; set; }

        [Required(ErrorMessage = "O telefone é obrigatório.")]
        [StringLength(20, ErrorMessage = "O telefone deve ter no máximo 20 caracteres.")]
        [RegularExpression(@"^(?:\([1-9][0-9]\)|[1-9][0-9])[ -]?[0-9]{4,5}-?[0-9]{4}$", ErrorMessage = "Informe o telefone com DDD (ex.: (21) 99999-9999).")]
        public required string Telefone { get; set; }

        [StringLength(300, ErrorMessage = "O endereço deve ter no máximo 300 caracteres.")]
        public string? Endereco { get; set; }
    }
}
