namespace GestaoMarmitaria.Api.Configurations
{
    public class JwtSettings
    {
        public const int TamanhoMinimoChave = 32;
        public const int ExpiracaoMinimaEmHoras = 1;
        public const int ExpiracaoMaximaEmHoras = 24;
        public const int ExpiracaoPadraoEmHoras = 2;

        public string ChaveSecreta { get; set; } = string.Empty;
        public string Emissor { get; set; } = string.Empty;
        public string Audiencia { get; set; } = string.Empty;
        public int ExpiracaoEmHoras { get; set; } = ExpiracaoPadraoEmHoras;

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

            if (ExpiracaoEmHoras < ExpiracaoMinimaEmHoras ||
                ExpiracaoEmHoras > ExpiracaoMaximaEmHoras)
            {
                throw new InvalidOperationException(
                    $"Configure Jwt:ExpiracaoEmHoras entre {ExpiracaoMinimaEmHoras} e {ExpiracaoMaximaEmHoras} horas.");
            }
        }
    }
}
