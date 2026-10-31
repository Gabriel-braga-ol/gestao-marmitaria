namespace GestaoMarmitaria.Api.Dtos
{
    public class ReembolsoRespostaDto
    {
        public int Id { get; set; }
        public int PagamentoId { get; set; }
        public decimal Valor { get; set; }
        public DateTime DataReembolso { get; set; }
    }
}