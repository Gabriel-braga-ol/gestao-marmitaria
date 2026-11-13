using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoMarmitaria.Api.Infra.Data.Configuracoes;

public class ConfiguracaoCliente : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.Property(c => c.Nome).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Telefone).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Endereco).HasMaxLength(300);
    }
}
