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
        public DbSet<Reembolso> Reembolsos { get; set; }
        public DbSet<ItemCardapio> ItensCardapio { get; set; }
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<HistoricoStatusPedido> HistoricosStatusPedido => Set<HistoricoStatusPedido>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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
                .WithOne(p => p.Pagamento)
                .HasForeignKey<Pagamento>(p => p.PedidoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Pagamento>()
                .HasOne(p => p.UsuarioRegistro)
                .WithMany()
                .HasForeignKey(p => p.UsuarioRegistroId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reembolso>()
                .Property(r => r.Valor)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Reembolso>()
                .HasOne(r => r.Pagamento)
                .WithOne(p => p.Reembolso)
                .HasForeignKey<Reembolso>(r => r.PagamentoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reembolso>()
                .HasOne(r => r.UsuarioRegistro)
                .WithMany()
                .HasForeignKey(r => r.UsuarioRegistroId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<HistoricoStatusPedido>(entity =>
            {
                entity.HasKey(h => h.Id);

                entity.HasOne(h => h.Pedido)
                    .WithMany()
                    .HasForeignKey(h => h.PedidoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(h => h.Usuario)
                    .WithMany()
                    .HasForeignKey(h => h.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ItemCardapio>()
                .HasOne(i => i.Produto)
                .WithMany()
                .HasForeignKey(i => i.ProdutoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ItemCardapio>()
                .HasIndex(i => new { i.Data, i.ProdutoId })
                .IsUnique();

            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Nome).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.SenhaHash).IsRequired();
                entity.Property(u => u.Perfil).IsRequired();
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Hash).IsRequired().HasMaxLength(64);
                entity.HasIndex(r => r.Hash).IsUnique();
                entity.HasIndex(r => r.UsuarioId);
                entity.HasIndex(r => r.FamiliaId);

                entity.HasOne(r => r.Usuario)
                    .WithMany()
                    .HasForeignKey(r => r.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.SubstituidoPor)
                    .WithMany()
                    .HasForeignKey(r => r.SubstituidoPorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
