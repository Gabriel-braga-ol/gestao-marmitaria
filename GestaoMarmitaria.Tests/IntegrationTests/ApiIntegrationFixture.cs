using System.Net.Http.Headers;
using System.Net.Http.Json;
using GestaoMarmitaria.Api.Infra.Configurations;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Features.Autenticacao;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GestaoMarmitaria.Tests.IntegrationTests;

/// <summary>
/// Sobe a API real (WebApplicationFactory) contra um banco PostgreSQL exclusivo,
/// criado pelas migrations no início da execução e apagado no fim.
/// </summary>
public sealed class ApiIntegrationFixture : IAsyncLifetime
{
    public const string EmailAdmin = "admin.integracao@marmitaria.test";
    public const string SenhaAdmin = "AdminIntegracao123!";
    public const string EmailFuncionario = "funcionario.integracao@marmitaria.test";
    public const string SenhaFuncionario = "Funcionario123!";
    public const decimal TaxaEntrega = 5.00m;

    private const string ChaveJwtTeste =
        "chave-jwt-de-teste-com-mais-de-32-caracteres-para-integracao";

    private readonly string _nomeBanco;
    private readonly string _connectionStringTeste;
    private readonly string _connectionStringAdministrativa;
    private readonly Dictionary<string, string?> _variaveisDeAmbienteOriginais = new();

    private WebApplicationFactory<Program>? _factory;
    private Task<string>? _tokenAdmin;
    private Task<string>? _tokenFuncionario;

    public ApiIntegrationFixture()
    {
        var configuracaoBase = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionStringOriginal = configuracaoBase.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionStringOriginal))
        {
            throw new InvalidOperationException(
                "Configure ConnectionStrings:DefaultConnection (User Secrets ou variável de ambiente) " +
                "para executar os testes de integração.");
        }

        _connectionStringAdministrativa = new NpgsqlConnectionStringBuilder(connectionStringOriginal)
        {
            Database = "postgres"
        }.ConnectionString;

        _nomeBanco = $"gm_test_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";

        var construtorTeste = new NpgsqlConnectionStringBuilder(connectionStringOriginal)
        {
            Database = _nomeBanco
        };

        _connectionStringTeste = construtorTeste.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        await CriarBancoDeTesteAsync();
        await AplicarMigrationsAsync();
        ConfigurarVariaveisDeAmbiente();

        _factory = new WebApplicationFactory<Program>();

        // Força a inicialização do host, que executa o seed do Admin.
        using var cliente = CriarCliente();
        await cliente.GetAsync("/api/status");
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        RestaurarVariaveisDeAmbiente();
        await ApagarBancoDeTesteAsync();
    }

    public HttpClient CriarCliente()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("A fixture ainda não foi inicializada.");
        }

        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public AppDbContext CriarDbContext()
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionStringTeste)
            .Options;

        return new AppDbContext(opcoes);
    }

    /// <summary>
    /// Cria uma API isolada sobre o mesmo banco de teste, com limites de login próprios.
    /// O rate limiter é por processo, então a instância dedicada não interfere na fixture principal.
    /// </summary>
    public WebApplicationFactory<Program> CriarFactoryComLimiteDeLogin(
        int limiteTentativas,
        int janelaEmSegundos = 60)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.Configure<RateLimitingSettings>(settings =>
                    {
                        settings.Login.LimiteTentativas = limiteTentativas;
                        settings.Login.JanelaEmSegundos = janelaEmSegundos;
                    })));
    }

    /// <summary>
    /// Sobe uma API com o ambiente Production para exercitar HTTPS/HSTS e AllowedHosts.
    /// </summary>
    public WebApplicationFactory<Program> CriarFactoryEmProducao()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder
                    .UseEnvironment("Production")
                    .UseSetting("HTTPS_PORT", "7163"));
    }

    public async Task DefinirDataCriacaoAsync(int pedidoId, DateTime dataCriacaoUtc)
    {
        await using var contexto = CriarDbContext();

        await contexto.Pedidos
            .Where(p => p.Id == pedidoId)
            .ExecuteUpdateAsync(propriedades => propriedades
                .SetProperty(p => p.DataCriacao, dataCriacaoUtc));
    }

    public Task<string> TokenAdminAsync()
    {
        return _tokenAdmin ??= AutenticarAsync(EmailAdmin, SenhaAdmin);
    }

    public Task<string> TokenFuncionarioAsync()
    {
        return _tokenFuncionario ??= ObterTokenFuncionarioAsync();
    }

    private async Task<string> ObterTokenFuncionarioAsync()
    {
        var tokenAdmin = await TokenAdminAsync();

        using var cliente = CriarCliente();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenAdmin);

        var resposta = await cliente.PostAsJsonAsync("/api/autenticacao/registrar", new
        {
            nome = "Funcionario Integracao",
            email = EmailFuncionario,
            senha = SenhaFuncionario,
            perfil = 2
        });

        if (resposta.StatusCode != System.Net.HttpStatusCode.Created &&
            resposta.StatusCode != System.Net.HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException(
                $"Não foi possível registrar o funcionário de teste: " +
                $"{(int)resposta.StatusCode} {await resposta.Content.ReadAsStringAsync()}");
        }

        return await AutenticarAsync(EmailFuncionario, SenhaFuncionario);
    }

    private async Task<string> AutenticarAsync(string email, string senha)
    {
        using var cliente = CriarCliente();

        var resposta = await cliente.PostAsJsonAsync("/api/autenticacao/login", new
        {
            email,
            senha
        });

        if (resposta.StatusCode != System.Net.HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Login de teste falhou para {email}: " +
                $"{(int)resposta.StatusCode} {await resposta.Content.ReadAsStringAsync()}");
        }

        var token = await resposta.Content.ReadFromJsonAsync<TokenRespostaDto>();

        return token!.Token;
    }

    private async Task CriarBancoDeTesteAsync()
    {
        try
        {
            await using var conexao = new NpgsqlConnection(_connectionStringAdministrativa);
            await conexao.OpenAsync();

            await using var comando = conexao.CreateCommand();
            comando.CommandText = $"CREATE DATABASE \"{_nomeBanco}\"";
            await comando.ExecuteNonQueryAsync();
        }
        catch (PostgresException ex)
        {
            throw new InvalidOperationException(
                $"Não foi possível criar o banco de teste '{_nomeBanco}'. " +
                "Verifique se o usuário do PostgreSQL tem permissão de CREATEDB.",
                ex);
        }
    }

    private async Task AplicarMigrationsAsync()
    {
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionStringTeste)
            .Options;

        await using var contexto = new AppDbContext(opcoes);
        await contexto.Database.MigrateAsync();
    }

    private async Task ApagarBancoDeTesteAsync()
    {
        NpgsqlConnection.ClearAllPools();

        try
        {
            await using var conexao = new NpgsqlConnection(_connectionStringAdministrativa);
            await conexao.OpenAsync();

            await using var comando = conexao.CreateCommand();
            comando.CommandText = $"DROP DATABASE IF EXISTS \"{_nomeBanco}\" WITH (FORCE)";
            await comando.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Banco de teste '{_nomeBanco}' não pôde ser apagado: {ex.Message}");
        }
    }

    private void ConfigurarVariaveisDeAmbiente()
    {
        DefinirVariavel("ConnectionStrings__DefaultConnection", _connectionStringTeste);
        DefinirVariavel("Jwt__ChaveSecreta", ChaveJwtTeste);
        DefinirVariavel("AdminInicial__Nome", "Admin Integracao");
        DefinirVariavel("AdminInicial__Email", EmailAdmin);
        DefinirVariavel("AdminInicial__Senha", SenhaAdmin);
        DefinirVariavel("RateLimiting__Login__LimiteTentativas", "1000");
        DefinirVariavel("RateLimiting__Login__JanelaEmSegundos", "60");
        DefinirVariavel("Logging__LogLevel__Default", "Warning");
        DefinirVariavel("Logging__LogLevel__Microsoft.AspNetCore", "Warning");
    }

    private void DefinirVariavel(string nome, string valor)
    {
        _variaveisDeAmbienteOriginais[nome] = Environment.GetEnvironmentVariable(nome);
        Environment.SetEnvironmentVariable(nome, valor);
    }

    private void RestaurarVariaveisDeAmbiente()
    {
        foreach (var (nome, valor) in _variaveisDeAmbienteOriginais)
        {
            Environment.SetEnvironmentVariable(nome, valor);
        }
    }
}
