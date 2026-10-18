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

        // Permite consultar e modificar os produtos por meio do EF Core. - corresponde à tabela produtos
        public DbSet<Produto> Produtos { get; set; }

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
        }
    }
}
