using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoMarmitaria.Api.Infra.Data.Configuracoes;

public class ConfiguracaoItemCardapio : IEntityTypeConfiguration<ItemCardapio>
{
    public void Configure(EntityTypeBuilder<ItemCardapio> builder)
    {
        builder
            .HasOne(i => i.Produto)
            .WithMany()
            .HasForeignKey(i => i.ProdutoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(i => new { i.Data, i.ProdutoId })
            .IsUnique()
            .HasDatabaseName(IndicesUnicos.ItemCardapioPorDataEProduto);
    }
}
