using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Models
{
    /// <summary>
    /// Rastro de cada transição de status do pedido, preservado mesmo quando o pedido é cancelado.
    /// </summary>
    public class HistoricoStatusPedido
    {
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public StatusPedido StatusAnterior { get; set; }
        public StatusPedido StatusNovo { get; set; }
        public int? UsuarioId { get; set; }
        public DateTime DataAlteracao { get; set; }
        public Pedido Pedido { get; set; } = null!;
        public Usuario? Usuario { get; set; }
    }
}
