using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoMarmitaria.Api.Infra.Data.Configuracoes;

public class ConfiguracaoPedido : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder
            .HasOne(p => p.Cliente)
            .WithMany()
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.EnderecoEntrega).HasMaxLength(300);
        builder.Property(p => p.Observacoes).HasMaxLength(500);
        builder.Property(p => p.TaxaEntrega).HasPrecision(6, 2);

        builder
            .HasIndex(p => p.DataCriacao)
            .HasDatabaseName("IX_Pedidos_DataCriacao");

        builder
            .HasIndex(p => p.Status)
            .HasDatabaseName("IX_Pedidos_Status");
    }
}
