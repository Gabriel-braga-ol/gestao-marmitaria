namespace GestaoMarmitaria.Api.Infra.Configurations;

public class RateLimitingSettings
{
    public LoginRateLimitSettings Login { get; set; } = new();

    public void Validar()
    {
        if (Login is null)
        {
            throw new InvalidOperationException("Configure RateLimiting:Login.");
        }

        if (Login.LimiteTentativas <= 0)
        {
            throw new InvalidOperationException(
                "Configure RateLimiting:Login:LimiteTentativas com um valor maior que zero.");
        }

        if (Login.JanelaEmSegundos <= 0)
        {
            throw new InvalidOperationException(
                "Configure RateLimiting:Login:JanelaEmSegundos com um valor maior que zero.");
        }
    }
}

public class LoginRateLimitSettings
{
    public int LimiteTentativas { get; set; } = 5;
    public int JanelaEmSegundos { get; set; } = 60;
}
