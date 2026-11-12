namespace GestaoMarmitaria.Api.Configurations
{
    public class JwtSettings
    {
        public const int TamanhoMinimoChave = 32;
        public const int ExpiracaoMinimaEmMinutos = 5;
        public const int ExpiracaoMaximaEmMinutos = 60;
        public const int ExpiracaoPadraoEmMinutos = 15;
        public const int ExpiracaoRefreshMinimaEmDias = 1;
        public const int ExpiracaoRefreshMaximaEmDias = 90;
        public const int ExpiracaoRefreshPadraoEmDias = 7;

        public string ChaveSecreta { get; set; } = string.Empty;
        public string Emissor { get; set; } = string.Empty;
        public string Audiencia { get; set; } = string.Empty;
        public int ExpiracaoEmMinutos { get; set; } = ExpiracaoPadraoEmMinutos;
        public int ExpiracaoRefreshEmDias { get; set; } = ExpiracaoRefreshPadraoEmDias;

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

            if (ExpiracaoEmMinutos < ExpiracaoMinimaEmMinutos ||
                ExpiracaoEmMinutos > ExpiracaoMaximaEmMinutos)
            {
                throw new InvalidOperationException(
                    $"Configure Jwt:ExpiracaoEmMinutos entre {ExpiracaoMinimaEmMinutos} e {ExpiracaoMaximaEmMinutos} minutos.");
            }

            if (ExpiracaoRefreshEmDias < ExpiracaoRefreshMinimaEmDias ||
                ExpiracaoRefreshEmDias > ExpiracaoRefreshMaximaEmDias)
            {
                throw new InvalidOperationException(
                    $"Configure Jwt:ExpiracaoRefreshEmDias entre {ExpiracaoRefreshMinimaEmDias} e {ExpiracaoRefreshMaximaEmDias} dias.");
            }
        }
    }
}
