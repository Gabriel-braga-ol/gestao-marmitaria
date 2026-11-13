namespace GestaoMarmitaria.Api.Infra.Regras;

public static class FusoBrasilia
{
    public const string Identificador = "America/Sao_Paulo";

    public static TimeZoneInfo Zona { get; } =
        TimeZoneInfo.FindSystemTimeZoneById(Identificador);

    public static DateTime ParaUtc(DateOnly data)
    {
        var inicioLocal = data.ToDateTime(
            TimeOnly.MinValue,
            DateTimeKind.Unspecified);

        // Na transição de horário de verão a meia-noite local pode não existir;
        // avança até o primeiro horário válido em vez de converter um instante inválido.
        while (Zona.IsInvalidTime(inicioLocal))
        {
            inicioLocal = inicioLocal.AddMinutes(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(inicioLocal, Zona);
    }

    public static DateOnly Hoje(TimeProvider timeProvider)
    {
        var agoraBrasilia = TimeZoneInfo.ConvertTimeFromUtc(
            timeProvider.GetUtcNow().UtcDateTime,
            Zona);

        return DateOnly.FromDateTime(agoraBrasilia);
    }
}
