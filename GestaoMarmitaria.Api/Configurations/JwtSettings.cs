namespace GestaoMarmitaria.Api.Configurations
{
    public class JwtSettings
    {
        public const int TamanhoMinimoChave = 32;

        public string ChaveSecreta { get; set; } = string.Empty;
        public string Emissor { get; set; } = string.Empty;
        public string Audiencia { get; set; } = string.Empty;
        public int ExpiracaoEmHoras { get; set; }

        public void Validar()
        {
            if (string.IsNullOrWhiteSpace(ChaveSecreta) || ChaveSecreta.Length < TamanhoMinimoChave)
            {
                throw new InvalidOperationException(
                    $"Configure Jwt:ChaveSecreta com pelo menos {TamanhoMinimoChave} caracteres via User Secrets ou variável de ambiente.");
            }

            if (string.IsNullOrWhiteSpace(Emissor))
            {
                throw new InvalidOperationException("Configure Jwt:Emissor.");
            }

            if (string.IsNullOrWhiteSpace(Audiencia))
            {
                throw new InvalidOperationException("Configure Jwt:Audiencia.");
            }

            if (ExpiracaoEmHoras <= 0)
            {
                throw new InvalidOperationException("Configure Jwt:ExpiracaoEmHoras com um valor maior que zero.");
            }
        }
    }
}
