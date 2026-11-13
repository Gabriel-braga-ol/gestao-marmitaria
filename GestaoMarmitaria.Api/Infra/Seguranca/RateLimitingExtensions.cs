using System.Threading.RateLimiting;
using GestaoMarmitaria.Api.Infra.Configurations;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GestaoMarmitaria.Api.Infra.Seguranca;

public static class RateLimitingExtensions
{
    public static IServiceCollection AdicionarRateLimitingLogin(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RateLimitingSettings>(
            configuration.GetSection("RateLimiting"));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("login", contexto =>
            {
                var login = contexto.RequestServices
                    .GetRequiredService<IOptionsMonitor<RateLimitingSettings>>()
                    .CurrentValue.Login;

                return RateLimitPartition.GetFixedWindowLimiter(
                    contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = login.LimiteTentativas,
                        Window = TimeSpan.FromSeconds(login.JanelaEmSegundos),

                        // Rejeita na hora em vez de enfileirar tentativas.
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.OnRejected = (contexto, _) =>
            {
                if (contexto.Lease.TryGetMetadata(
                    MetadataName.RetryAfter,
                    out var retryAfter))
                {
                    contexto.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }

                return new ValueTask(contexto.HttpContext.Response.WriteAsJsonAsync(
                    new { mensagem = "Muitas tentativas de login. Tente novamente mais tarde." }));
            };
        });

        return services;
    }
}
