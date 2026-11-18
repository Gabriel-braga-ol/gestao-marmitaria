using System.Linq;
using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Features.Pedidos
{
    public class PedidoRespostaDto
    {
        public int Id { get; init; }
        public int ClienteId { get; init; }
        public DateTime DataCriacao { get; init; }
        public TipoEntrega TipoEntrega { get; init; }
        public string? EnderecoEntrega { get; init; }
        public decimal TaxaEntrega { get; init; }
        public string? Observacoes { get; init; }
        public StatusPedido Status { get; init; }
        public List<ItemPedidoRespostaDto> Itens { get; init; } = new();
        public decimal Total => Itens.Sum(i => i.Subtotal) + TaxaEntrega;
    }
}
