namespace GestaoMarmitaria.Api.Infra.Configurations
{
    public class LimpezaRefreshTokensSettings
    {
        public const int IntervaloMinimoEmHoras = 1;
        public const int IntervaloMaximoEmHoras = 168;
        public const int IntervaloPadraoEmHoras = 24;
        public const int RetencaoMinimaEmDias = 1;
        public const int RetencaoMaximaEmDias = 365;
        public const int RetencaoPadraoEmDias = 30;

        public int IntervaloEmHoras { get; set; } = IntervaloPadraoEmHoras;
        public int RetencaoEmDias { get; set; } = RetencaoPadraoEmDias;

        public void Validar()
        {
            if (IntervaloEmHoras < IntervaloMinimoEmHoras ||
                IntervaloEmHoras > IntervaloMaximoEmHoras)
            {
                throw new InvalidOperationException(
                    $"Configure LimpezaRefreshTokens:IntervaloEmHoras entre {IntervaloMinimoEmHoras} e {IntervaloMaximoEmHoras} horas.");
            }

            if (RetencaoEmDias < RetencaoMinimaEmDias ||
                RetencaoEmDias > RetencaoMaximaEmDias)
            {
                throw new InvalidOperationException(
                    $"Configure LimpezaRefreshTokens:RetencaoEmDias entre {RetencaoMinimaEmDias} e {RetencaoMaximaEmDias} dias.");
            }
        }
    }
}
