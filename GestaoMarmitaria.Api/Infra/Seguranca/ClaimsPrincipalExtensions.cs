using System.Security.Claims;

namespace GestaoMarmitaria.Api.Infra.Seguranca;

public static class ClaimsPrincipalExtensions
{
    public static int? ObterUsuarioId(this ClaimsPrincipal principal)
    {
        var valor = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(valor, out var usuarioId) ? usuarioId : null;
    }
}
