using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoMarmitaria.Api.Infra.Data.Configuracoes;

public class ConfiguracaoRefreshToken : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Hash).IsRequired().HasMaxLength(64);
        builder.HasIndex(r => r.Hash).IsUnique();
        builder.HasIndex(r => r.UsuarioId);
        builder.HasIndex(r => r.FamiliaId);

        builder
            .HasOne(r => r.Usuario)
            .WithMany()
            .HasForeignKey(r => r.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(r => r.SubstituidoPor)
            .WithMany()
            .HasForeignKey(r => r.SubstituidoPorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
