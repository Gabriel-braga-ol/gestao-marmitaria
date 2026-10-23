using System.ComponentModel.DataAnnotations;
using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Dtos
{
    public class AtualizarStatusPedidoDto
    {
        [EnumDataType(typeof(StatusPedido), ErrorMessage = "O status informado é inválido.")]
        public StatusPedido Status { get; set; }
    }
}
