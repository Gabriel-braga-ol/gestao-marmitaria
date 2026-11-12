using GestaoMarmitaria.Api.Enums;
using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Features.Pagamentos
{
    public class RegistrarPagamentoDto
    {
        [EnumDataType(typeof(FormaPagamento), ErrorMessage = "A forma de pagamento informada é inválida.")]
        public FormaPagamento FormaPagamento { get; set; }
    }
}
