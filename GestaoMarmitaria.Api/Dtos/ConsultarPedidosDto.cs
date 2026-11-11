using System.ComponentModel.DataAnnotations;
using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Dtos
{
    public class ConsultarPedidosDto : PaginacaoDto, IValidatableObject
    {
        [EnumDataType(typeof(StatusPedido), ErrorMessage = "O status informado é inválido.")]
        public StatusPedido? Status { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "O ID do cliente deve ser maior que zero.")]
        public int? ClienteId { get; set; }

        public DateOnly? DataInicial { get; set; }
        public DateOnly? DataFinal { get; set; }
        public bool? Pago { get; set; }
        public bool? ReembolsoPendente { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DataInicial.HasValue && DataFinal.HasValue && DataInicial.Value > DataFinal.Value)
            {
                yield return new ValidationResult("A data inicial não pode ser posterior à data final.",
                    new[] { nameof(DataInicial), nameof(DataFinal) });
            }
        }
    }
}
