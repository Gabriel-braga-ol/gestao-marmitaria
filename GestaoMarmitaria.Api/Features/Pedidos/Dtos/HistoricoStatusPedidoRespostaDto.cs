using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Features.Pedidos
{
    public class HistoricoStatusPedidoRespostaDto
    {
        public int Id { get; set; }
        public StatusPedido StatusAnterior { get; set; }
        public StatusPedido StatusNovo { get; set; }
        public int? UsuarioId { get; set; }
        public string? UsuarioNome { get; set; }
        public DateTime DataAlteracao { get; set; }
    }
}
