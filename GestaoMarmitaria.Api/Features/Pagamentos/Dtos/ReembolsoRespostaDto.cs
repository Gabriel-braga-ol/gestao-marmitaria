namespace GestaoMarmitaria.Api.Features.Pagamentos
{
    public class ReembolsoRespostaDto
    {
        public int Id { get; init; }
        public int PagamentoId { get; init; }
        public decimal Valor { get; init; }
        public DateTime DataReembolso { get; init; }
        public int? UsuarioRegistroId { get; init; }
        public string? UsuarioRegistroNome { get; init; }
        public DateTime? DataRegistro { get; init; }
    }
}
