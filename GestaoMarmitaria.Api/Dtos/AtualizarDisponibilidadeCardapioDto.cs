using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Dtos
{
    public class AtualizarDisponibilidadeCardapioDto
    {
        [Required(ErrorMessage = "A disponibilidade é obrigatória.")]
        public bool? Disponivel { get; set; }
    }
}