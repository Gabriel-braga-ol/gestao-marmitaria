using System.ComponentModel.DataAnnotations;
using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Dtos
{
    public class ConsultarPedidosDto : IValidatableObject
    {
        [EnumDataType(typeof(StatusPedido), ErrorMessage = "O status informado é inválido.")]
        public StatusPedido? Status { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "O ID do cliente deve ser maior que zero.")]
        public int? ClienteId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior que zero.")]
        public int Pagina { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "O tamanho da página deve estar entre 1 e 100.")]
        public int TamanhoPagina { get; set; } = 20;
        public DateOnly? DataInicial { get; set; }
        public DateOnly? DataFinal { get; set; }

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
