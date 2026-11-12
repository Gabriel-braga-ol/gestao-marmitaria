namespace GestaoMarmitaria.Api.Services
{
    public interface ITokenService
    {
        TokenGerado GerarToken(string usuarioId, string email, string perfil);
    }
}
