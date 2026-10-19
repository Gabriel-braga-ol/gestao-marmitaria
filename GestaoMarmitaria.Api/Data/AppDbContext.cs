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
        }
    }
}
