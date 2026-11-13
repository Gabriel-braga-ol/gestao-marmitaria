namespace GestaoMarmitaria.Api.Infra.Regras;

/// <summary>
/// Forma canônica do e-mail usada no cadastro, no login e no seed.
/// </summary>
public static class NormalizacaoEmail
{
    public static string Normalizar(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
