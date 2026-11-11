namespace GestaoMarmitaria.Api.Configurations;

/// <summary>
/// Regras de senha compartilhadas entre o cadastro HTTP e o seed do Admin inicial.
/// </summary>
public static class PoliticaSenha
{
    public const int TamanhoMinimo = 10;
    public const int TamanhoMaximo = 128;
}
