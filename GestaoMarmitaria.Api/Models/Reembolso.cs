namespace GestaoMarmitaria.Api.Models
{
    public class Reembolso
    {
        public int Id { get; set; }
        public int PagamentoId { get; set; }
        public decimal Valor { get; set; }
        public DateTime DataReembolso { get; set; } = DateTime.UtcNow;
        public Pagamento Pagamento { get; set; } = null!;
    }
}