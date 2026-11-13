using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Models;
using GestaoMarmitaria.Api.Infra.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestaoMarmitaria.Tests
{
    public class AdminInicialSeederTests
    {
        private static (ServiceProvider Provider, IConfiguration Configuration) CriarAmbiente(
            string email = "admin@marmitaria.com",
            string senha = "senhaSegura123",
            string nome = "Administrador")
        {
            var services = new ServiceCollection();
            var nomeBanco = Guid.NewGuid().ToString();

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(nomeBanco));

            services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

            var provider = services.BuildServiceProvider();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AdminInicial:Nome"] = nome,
                    ["AdminInicial:Email"] = email,
                    ["AdminInicial:Senha"] = senha
                })
                .Build();

            return (provider, configuration);
        }

        [Fact]
        public async Task ExecutarAsync_SemAdmin_DeveCriarAdminComSenhaHasheada()
        {
            var (provider, configuration) = CriarAmbiente();

            await AdminInicialSeeder.ExecutarAsync(
                provider,
                configuration,
                NullLogger.Instance);

            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await context.Usuarios.SingleAsync();

            Assert.Equal(PerfilUsuario.Admin, admin.Perfil);
            Assert.Equal("admin@marmitaria.com", admin.Email);

            var passwordHasher = new PasswordHasher<Usuario>();
            var resultado = passwordHasher.VerifyHashedPassword(
                admin,
                admin.SenhaHash,
                "senhaSegura123");

            Assert.NotEqual(PasswordVerificationResult.Failed, resultado);
        }

        [Fact]
        public async Task ExecutarAsync_DeveNormalizarEmailDoAdmin()
        {
            var (provider, configuration) = CriarAmbiente(email: "  Admin@Marmitaria.COM  ");

            await AdminInicialSeeder.ExecutarAsync(
                provider,
                configuration,
                NullLogger.Instance);

            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await context.Usuarios.SingleAsync();

            Assert.Equal("admin@marmitaria.com", admin.Email);
        }

        [Fact]
        public async Task ExecutarAsync_JaExisteAdmin_NaoDeveCriarOutro()
        {
            var (provider, configuration) = CriarAmbiente();

            await AdminInicialSeeder.ExecutarAsync(provider, configuration, NullLogger.Instance);
            await AdminInicialSeeder.ExecutarAsync(provider, configuration, NullLogger.Instance);

            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.Equal(1, await context.Usuarios.CountAsync());
        }

        [Fact]
        public async Task ExecutarAsync_EmailJaCadastrado_NaoDeveCriarAdmin()
        {
            var (provider, configuration) = CriarAmbiente();

            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                context.Usuarios.Add(new Usuario
                {
                    Nome = "Funcionario",
                    Email = "admin@marmitaria.com",
                    Perfil = PerfilUsuario.Funcionario,
                    SenhaHash = "hash-simulado"
                });
                await context.SaveChangesAsync();
            }

            await AdminInicialSeeder.ExecutarAsync(provider, configuration, NullLogger.Instance);

            using var scopeVerificacao = provider.CreateScope();
            var contextVerificacao = scopeVerificacao.ServiceProvider.GetRequiredService<AppDbContext>();
            var usuarios = await contextVerificacao.Usuarios.ToListAsync();

            Assert.Single(usuarios);
            Assert.DoesNotContain(usuarios, u => u.Perfil == PerfilUsuario.Admin);
        }
    }
}
