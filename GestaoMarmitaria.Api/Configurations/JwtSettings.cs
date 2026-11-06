namespace GestaoMarmitaria.Api.Configurations
{
    public class JwtSettings
    {
        public string ChaveSecreta { get; set; } = string.Empty;
        public string Emissor { get; set; } = string.Empty;
        public string Audiencia { get; set; } = string.Empty;
        public int ExpiracaoEmHoras { get; set; }
    }
}
