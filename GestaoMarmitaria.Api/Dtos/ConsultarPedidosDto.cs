using System.ComponentModel.DataAnnotations;
using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Dtos
{
    public class ConsultarPedidosDto
    {
        [EnumDataType(typeof(StatusPedido), ErrorMessage = "O status informado é inválido.")]
        public StatusPedido? Status { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "O ID do cliente deve ser maior que zero.")]
        public int? ClienteId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior que zero.")]
        public int Pagina { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "O tamanho da página deve estar entre 1 e 100.")]
        public int TamanhoPagina { get; set; } = 20;
    }
}
