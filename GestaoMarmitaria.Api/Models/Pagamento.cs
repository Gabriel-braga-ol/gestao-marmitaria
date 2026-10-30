using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Models
{
    public class Pagamento
    {
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public FormaPagamento FormaPagamento { get; set; }
        public decimal Valor { get; set; }
        public DateTime DataPagamento { get; set; } = DateTime.UtcNow;
        public Pedido Pedido { get; set; } = null!;

    }
}
