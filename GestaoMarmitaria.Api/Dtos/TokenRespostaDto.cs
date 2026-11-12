namespace GestaoMarmitaria.Api.Dtos
{
    public class TokenRespostaDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiraEm { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshExpiraEm { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Perfil { get; set; } = string.Empty;
    }
}
