using System.ComponentModel.DataAnnotations;
using System.Linq;
using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Dtos
{
    public class CriarPedidoDto : IValidatableObject
    {
        [Range(1, int.MaxValue, ErrorMessage = "O ClienteId deve ser maior que zero.")]
        public int ClienteId { get; set; }

        [Required(ErrorMessage = "Os itens do pedido são obrigatórios.")]
        [MinLength(1, ErrorMessage = "O pedido deve conter pelo menos um item.")]
        public List<CriarItemPedidoDto> Itens { get; set; } = new();

        [EnumDataType(typeof(TipoEntrega), ErrorMessage = "O tipo de entrega é inválido.")]
        public TipoEntrega TipoEntrega { get; set; }

        [StringLength(300, ErrorMessage = "O endereço deve ter no máximo 300 caracteres.")]
        public string? EnderecoEntrega { get; set; }

        [StringLength(500, ErrorMessage = "As observações devem ter no máximo 500 caracteres.")]
        public string? Observacoes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (TipoEntrega == GestaoMarmitaria.Api.Enums.TipoEntrega.Entrega &&
                string.IsNullOrWhiteSpace(EnderecoEntrega))
            {
                yield return new ValidationResult(
                    "O endereço é obrigatório para entrega.",
                    new[] { nameof(EnderecoEntrega) });
            }

            if (Itens is not null && Itens.Any(i => i is null))
            {
                yield return new ValidationResult(
                    "O pedido não pode conter itens nulos.",
                    new[] { nameof(Itens) });

                yield break;
            }

            if (Itens is not null && Itens.Select(i => i.ProdutoId).Distinct().Count() < Itens.Count)
            {
                yield return new ValidationResult(
                    "O pedido não pode conter produtos repetidos. Ajuste a quantidade do item.",
                    new[] { nameof(Itens) });
            }
        }
    }
}
