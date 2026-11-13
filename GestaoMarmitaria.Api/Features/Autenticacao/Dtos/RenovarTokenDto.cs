using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Features.Autenticacao
{
    public class RenovarTokenDto
    {
        [Required]
        [StringLength(200, MinimumLength = 20)]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
