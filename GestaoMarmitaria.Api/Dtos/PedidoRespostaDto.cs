using System.Linq;
using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Dtos
{
    public class PedidoRespostaDto
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public DateTime DataCriacao { get; set; }
        public TipoEntrega TipoEntrega { get; set; }
        public string? EnderecoEntrega { get; set; }
        public decimal TaxaEntrega { get; set; }
        public string? Observacoes { get; set; }
        public StatusPedido Status { get; set; }
        public List<ItemPedidoRespostaDto> Itens { get; set; } = new();
        public decimal Total => Itens.Sum(i => i.Subtotal) + TaxaEntrega;
    }
}
