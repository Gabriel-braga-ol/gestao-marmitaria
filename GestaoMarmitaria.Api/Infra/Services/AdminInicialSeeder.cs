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
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var adminSettings = CarregarSettings(configuration);

        if (!adminSettings.PossuiConfiguracao)
        {
            return;
        }

        using var scope = services.CreateScope();

        await GarantirAdminAsync(
            scope.ServiceProvider,
            adminSettings,
            logger,
            cancellationToken);
    }

    private static AdminInicialSettings CarregarSettings(
        IConfiguration configuration)
    {
        var adminSettings = configuration
            .GetSection("AdminInicial")
            .Get<AdminInicialSettings>() ?? new AdminInicialSettings();

        adminSettings.Validar();

        return adminSettings;
    }

    private static async Task GarantirAdminAsync(
        IServiceProvider services,
        AdminInicialSettings adminSettings,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var context = services.GetRequiredService<AppDbContext>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher<Usuario>>();

        var adminJaExiste = await context.Usuarios
            .AnyAsync(u => u.Perfil == PerfilUsuario.Admin, cancellationToken);

        if (adminJaExiste)
        {
            logger.LogInformation("Seed do Admin inicial ignorado: já existe um usuário Admin.");
            return;
        }

        var email = NormalizacaoEmail.Normalizar(adminSettings.Email);

        var emailJaCadastrado = await context.Usuarios
            .AnyAsync(u => u.Email == email, cancellationToken);

        if (emailJaCadastrado)
        {
            logger.LogWarning(
                "Seed do Admin inicial ignorado: o e-mail {Email} já está cadastrado.",
                email);
            return;
        }

        var usuario = new Usuario
        {
            Nome = adminSettings.Nome.Trim(),
            Email = email,
            Perfil = PerfilUsuario.Admin
        };

        usuario.SenhaHash = passwordHasher.HashPassword(usuario, adminSettings.Senha);

        context.Usuarios.Add(usuario);

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Admin inicial criado com o e-mail {Email}.",
                email);
        }
        catch (DbUpdateException ex) when (
            FalhasBanco.EhViolacaoDeIndiceUnico(ex, IndicesUnicos.UsuarioPorEmail))
        {
            logger.LogWarning(
                "Seed do Admin inicial ignorado: o e-mail {Email} já está cadastrado.",
                email);
        }
    }
}
