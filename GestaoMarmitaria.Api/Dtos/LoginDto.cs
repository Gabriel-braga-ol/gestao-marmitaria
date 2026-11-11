using GestaoMarmitaria.Api.Regras;
using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Dtos
{
    public class LoginDto
    {
        private string _email = string.Empty;

        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "O e-mail informado é inválido.")]
        public string Email
        {
            get => _email;
            set => _email = NormalizacaoEmail.Normalizar(value ?? string.Empty);
        }

        [Required(ErrorMessage = "A senha é obrigatória.")]
        public string Senha { get; set; } = string.Empty;
    }
}
