namespace GestaoMarmitaria.Api.Models
{
    public class Cliente
    {
        public int Id { get; set; } 
        public required string Nome { get; set; }
        public required string Telefone { get; set; }
        public string? Endereco { get; set; }
    }
}
