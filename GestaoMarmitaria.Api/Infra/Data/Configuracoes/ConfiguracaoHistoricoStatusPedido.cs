using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoMarmitaria.Api.Infra.Data.Configuracoes;

public class ConfiguracaoHistoricoStatusPedido
    : IEntityTypeConfiguration<HistoricoStatusPedido>
{
    public void Configure(EntityTypeBuilder<HistoricoStatusPedido> builder)
    {
        builder.HasKey(h => h.Id);

        builder
            .HasOne(h => h.Pedido)
            .WithMany()
            .HasForeignKey(h => h.PedidoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(h => h.Usuario)
            .WithMany()
            .HasForeignKey(h => h.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
