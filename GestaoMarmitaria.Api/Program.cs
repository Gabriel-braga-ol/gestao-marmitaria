using GestaoMarmitaria.Api.Infra.Configurations;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Infra.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var jwtSettingsSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtSettings>(jwtSettingsSection);

var jwtSettings = jwtSettingsSection.Get<JwtSettings>()
    ?? throw new InvalidOperationException("A seção Jwt não foi configurada.");

jwtSettings.Validar();

var rateLimitingSettings = builder.Configuration
    .GetSection("RateLimiting")
    .Get<RateLimitingSettings>() ?? new RateLimitingSettings();

rateLimitingSettings.Validar();

var limpezaRefreshTokensSettings = builder.Configuration
    .GetSection("LimpezaRefreshTokens")
    .Get<LimpezaRefreshTokensSettings>() ?? new LimpezaRefreshTokensSettings();

limpezaRefreshTokensSettings.Validar();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.Configure<RateLimitingSettings>(
    builder.Configuration.GetSection("RateLimiting"));

builder.Services.Configure<LimpezaRefreshTokensSettings>(
    builder.Configuration.GetSection("LimpezaRefreshTokens"));

builder.Services.AddRateLimiter(options =>
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
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.OnRejected = (contexto, _) =>
    {
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            contexto.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        return new ValueTask(contexto.HttpContext.Response.WriteAsJsonAsync(
            new { mensagem = "Muitas tentativas de login. Tente novamente mais tarde." }));
    };
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<ILimpezaRefreshTokensService, LimpezaRefreshTokensService>();
builder.Services.AddHostedService<LimpezaRefreshTokensHostedService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.ChaveSecreta)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Emissor,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audiencia,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

var app = builder.Build();

if (!EF.IsDesignTime)
{
    await AdminInicialSeeder.ExecutarAsync(app.Services, app.Configuration, app.Logger);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Fora de Development a API exige HTTPS. Atrás de proxy reverso, configure
    // ForwardedHeaders (X-Forwarded-Proto/X-Forwarded-For) no deploy.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Necessário para os testes de integração com WebApplicationFactory<Program>.
public partial class Program { }
