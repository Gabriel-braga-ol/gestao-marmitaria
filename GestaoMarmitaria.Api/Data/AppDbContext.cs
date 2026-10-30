using Microsoft.EntityFrameworkCore;
using GestaoMarmitaria.Api.Models;


namespace GestaoMarmitaria.Api.Data
{
    public class AppDbContext : DbContext
    {
        // Recebe as configurações do contexto e as encaminha à classe base.
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Coleção de todas as entidades
        public DbSet<Produto> Produtos { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<ItemPedido> ItensPedido { get; set; }
        public DbSet<Pagamento> Pagamentos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Produto>()
                .Property(p => p.Nome)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Produto>()
                .Property(p => p.Descricao)
                .HasMaxLength(500);

            modelBuilder.Entity<Produto>()
                .Property(p => p.Preco)
                .HasPrecision(6, 2);

            modelBuilder.Entity<Cliente>()
                .Property(c => c.Nome)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Cliente>()
                .Property(c => c.Telefone)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<Cliente>()
                .Property(c => c.Endereco)
                .HasMaxLength(300);

            modelBuilder.Entity<Pedido>()
                .HasOne(p => p.Cliente)
                .WithMany()
                .HasForeignKey(p => p.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ItemPedido>()
                .HasOne(i => i.Produto)
                .WithMany()
                .HasForeignKey(i => i.ProdutoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ItemPedido>()
                .HasOne(i => i.Pedido)
                .WithMany(p => p.Itens)
                .HasForeignKey(i => i.PedidoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Pedido>()
                .Property(p => p.EnderecoEntrega)
                .HasMaxLength(300);

            modelBuilder.Entity<Pedido>()
                .Property(p => p.Observacoes)
                .HasMaxLength(500);

            modelBuilder.Entity<ItemPedido>()
                .Property(i => i.NomeProduto)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Pedido>()
                .Property(p => p.TaxaEntrega)
                .HasPrecision(6, 2);

            modelBuilder.Entity<ItemPedido>()
                .Property(i => i.PrecoUnitario)
                .HasPrecision(6, 2);

            modelBuilder.Entity<Pagamento>()
                .Property(p => p.Valor)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Pagamento>()
                .HasOne(p => p.Pedido)
                .WithOne()
                .HasForeignKey<Pagamento>(p => p.PedidoId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
