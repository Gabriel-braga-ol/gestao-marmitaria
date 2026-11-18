namespace GestaoMarmitaria.Api.Features.Autenticacao
{
    public class TokenRespostaDto
    {
        public string Token { get; init; } = string.Empty;
        public DateTime ExpiraEm { get; init; }
        public string RefreshToken { get; init; } = string.Empty;
        public DateTime RefreshExpiraEm { get; init; }
        public string Email { get; init; } = string.Empty;
        public string Perfil { get; init; } = string.Empty;
    }
}
