namespace GestaoMarmitaria.Api.Models
{
    public class Reembolso
    {
        public int Id { get; set; }
        public int PagamentoId { get; set; }
        public decimal Valor { get; set; }
        public DateTime DataReembolso { get; set; } = DateTime.UtcNow;
        public int? UsuarioRegistroId { get; set; }
        public DateTime? DataRegistro { get; set; }
        public Pagamento Pagamento { get; set; } = null!;
        public Usuario? UsuarioRegistro { get; set; }
    }
}