using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Models
{
    public class Pedido
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public DateTime DataCriacao { get; set; }
        public TipoEntrega TipoEntrega { get; set; }
        public string? EnderecoEntrega { get; set; }
        public decimal TaxaEntrega { get; set; }
        public string? Observacoes { get; set; }
        public StatusPedido Status { get; set; } = StatusPedido.Novo;
        public ICollection<ItemPedido> Itens { get; set; } = new List<ItemPedido>();
        public Cliente Cliente { get; set; } = null!;
        public Pagamento? Pagamento { get; set; }
    }
}
