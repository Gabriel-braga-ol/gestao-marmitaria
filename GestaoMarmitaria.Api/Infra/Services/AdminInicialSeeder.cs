using GestaoMarmitaria.Api.Infra.Configurations;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Models;
using GestaoMarmitaria.Api.Infra.Regras;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Api.Infra.Services;

public static class AdminInicialSeeder
{
    public static async Task ExecutarAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        var settings = configuration
            .GetSection("AdminInicial")
            .Get<AdminInicialSettings>() ?? new AdminInicialSettings();

        settings.Validar();

        if (!settings.PossuiConfiguracao)
        {
            return;
        }

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<Usuario>>();

        var adminExiste = await context.Usuarios
            .AnyAsync(u => u.Perfil == PerfilUsuario.Admin);

        if (adminExiste)
        {
            logger.LogInformation("Seed do Admin inicial ignorado: já existe um usuário Admin.");
            return;
        }

        var email = NormalizacaoEmail.Normalizar(settings.Email);

        var emailExiste = await context.Usuarios
            .AnyAsync(u => u.Email == email);

        if (emailExiste)
        {
            logger.LogWarning(
                "Seed do Admin inicial ignorado: o e-mail {Email} já está cadastrado.",
                email);
            return;
        }

        var usuario = new Usuario
        {
            Nome = settings.Nome.Trim(),
            Email = email,
            Perfil = PerfilUsuario.Admin
        };

        usuario.SenhaHash = passwordHasher.HashPassword(usuario, settings.Senha);

        context.Usuarios.Add(usuario);

        try
        {
            await context.SaveChangesAsync();

            logger.LogInformation(
                "Admin inicial criado com o e-mail {Email}.",
                email);
        }
        catch (DbUpdateException ex) when (
            FalhasBanco.EhViolacaoDeIndiceUnico(ex, "IX_Usuarios_Email"))
        {
            logger.LogWarning(
                "Seed do Admin inicial ignorado: o e-mail {Email} já está cadastrado.",
                email);
        }
    }
}
