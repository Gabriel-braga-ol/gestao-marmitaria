using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoMarmitaria.Api.Infra.Data.Configuracoes;

public class ConfiguracaoUsuario : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Nome).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(150);

        builder
            .HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName(IndicesUnicos.UsuarioPorEmail);

        builder.Property(u => u.SenhaHash).IsRequired();
        builder.Property(u => u.Perfil).IsRequired();
    }
}
