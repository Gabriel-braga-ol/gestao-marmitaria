using GestaoMarmitaria.Api.Enums;
using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Dtos
{
    public class RegistrarUsuarioDto
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(100, ErrorMessage = "O nome deve conter no máximo 100 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail é obrigatório.")]
        [EmailAddress(ErrorMessage = "O e-mail informado é inválido.")]
        [StringLength(150, ErrorMessage = "O e-mail deve conter no máximo 150 caracteres.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "A senha é obrigatória.")]
        [MinLength(6, ErrorMessage = "A senha deve ter pelo menos 6 caracteres.")]
        public string Senha { get; set; } = string.Empty;

        [EnumDataType(typeof(PerfilUsuario), ErrorMessage = "Perfil de utilizador inválido.")]
        public PerfilUsuario Perfil { get; set; }
    }
}
