namespace GestaoMarmitaria.Api.Features.Pagamentos
{
    public class ReembolsoRespostaDto
    {
        public int Id { get; set; }
        public int PagamentoId { get; set; }
        public decimal Valor { get; set; }
        public DateTime DataReembolso { get; set; }
        public int? UsuarioRegistroId { get; set; }
        public string? UsuarioRegistroNome { get; set; }
        public DateTime? DataRegistro { get; set; }
    }
}
