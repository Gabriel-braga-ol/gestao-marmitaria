using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Features.Relatorios;

public class ConsultarRelatorioFinanceiroDto : IValidatableObject
{
    [Required(ErrorMessage = "A data inicial é obrigatória.")]
    public DateOnly? DataInicial { get; set; }

    [Required(ErrorMessage = "A data final é obrigatória.")]
    public DateOnly? DataFinal { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DataInicial.HasValue && DataFinal.HasValue && DataInicial.Value > DataFinal.Value)
        {
            yield return new ValidationResult(
                "A data inicial não pode ser posterior à data final.",
                new[] { nameof(DataInicial), nameof(DataFinal) });
        }
    }
}
