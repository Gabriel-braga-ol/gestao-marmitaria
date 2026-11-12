namespace GestaoMarmitaria.Api.Features.Clientes
{
    public class ClienteRespostaDto
    {
        public int Id { get; set; }
        public required string Nome { get; set; }
        public required string Telefone { get; set; }
        public string? Endereco { get; set; }
    }
}
