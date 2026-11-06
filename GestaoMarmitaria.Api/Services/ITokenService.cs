namespace GestaoMarmitaria.Api.Services
{
    public interface ITokenService
    {
        string GerarToken(string usuarioId, string email, string perfil);
    }
}
