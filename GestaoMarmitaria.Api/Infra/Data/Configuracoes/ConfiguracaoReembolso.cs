using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoMarmitaria.Api.Infra.Data.Configuracoes;

public class ConfiguracaoReembolso : IEntityTypeConfiguration<Reembolso>
{
    public void Configure(EntityTypeBuilder<Reembolso> builder)
    {
        builder.Property(r => r.Valor).HasPrecision(18, 2);

        builder
            .HasOne(r => r.Pagamento)
            .WithOne(p => p.Reembolso)
            .HasForeignKey<Reembolso>(r => r.PagamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(r => r.PagamentoId)
            .IsUnique()
            .HasDatabaseName(IndicesUnicos.ReembolsoPorPagamento);

        builder
            .HasOne(r => r.UsuarioRegistro)
            .WithMany()
            .HasForeignKey(r => r.UsuarioRegistroId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
