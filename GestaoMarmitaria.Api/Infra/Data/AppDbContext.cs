using Microsoft.EntityFrameworkCore;
using GestaoMarmitaria.Api.Models;


namespace GestaoMarmitaria.Api.Infra.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

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
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
