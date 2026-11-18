namespace GestaoMarmitaria.Api.Features.Clientes
{
    public class ClienteRespostaDto
    {
        public int Id { get; init; }
        public required string Nome { get; init; }
        public required string Telefone { get; init; }
        public string? Endereco { get; init; }
    }
}
