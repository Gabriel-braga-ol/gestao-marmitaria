using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoMarmitaria.Api.Infra.Data.Configuracoes;

public class ConfiguracaoPagamento : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.Property(p => p.Valor).HasPrecision(18, 2);

        builder
            .HasOne(p => p.Pedido)
            .WithOne(p => p.Pagamento)
            .HasForeignKey<Pagamento>(p => p.PedidoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(p => p.PedidoId)
            .IsUnique()
            .HasDatabaseName(IndicesUnicos.PagamentoPorPedido);

        builder
            .HasOne(p => p.UsuarioRegistro)
            .WithMany()
            .HasForeignKey(p => p.UsuarioRegistroId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
