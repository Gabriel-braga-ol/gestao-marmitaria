namespace GestaoMarmitaria.Api.Infra.Regras;

public static class NormalizacaoTelefone
{
    public static string Normalizar(string telefone)
    {
        return new string(telefone.Where(char.IsDigit).ToArray());
    }
}
