namespace GestaoMarmitaria.Api.Infra.Services
{
    public interface ITokenService
    {
        TokenGerado GerarToken(string usuarioId, string email, string perfil);
    }
}
