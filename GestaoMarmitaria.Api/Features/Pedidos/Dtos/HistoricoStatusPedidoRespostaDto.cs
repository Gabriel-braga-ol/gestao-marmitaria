using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Features.Pedidos
{
    public class HistoricoStatusPedidoRespostaDto
    {
        public int Id { get; init; }
        public StatusPedido StatusAnterior { get; init; }
        public StatusPedido StatusNovo { get; init; }
        public int? UsuarioId { get; init; }
        public string? UsuarioNome { get; init; }
        public DateTime DataAlteracao { get; init; }
    }
}
