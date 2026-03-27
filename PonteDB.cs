using Microsoft.EntityFrameworkCore;
using ClassesBSFM;
using System;

namespace PonteBanco
{
    public class BSFMContext : DbContext
    {
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Refeição> Refeicoes { get; set; }
        public DbSet<Comida> Comidas { get; set; }
        public DbSet<CronogramaAlimentar> Cronogramas { get; set; }
        public DbSet<Hospital> Hospitais { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var connectionUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            if (string.IsNullOrEmpty(connectionUrl))
            {
                options.UseSqlite("Data Source=UsuariosBSFM.db");
            }
            else
            {
                // Limpeza e ajuste da URL do Railway para o Npgsql
                connectionUrl = connectionUrl.Replace("postgres://", "postgresql://");
                options.UseNpgsql(connectionUrl, npgsqlOptions => {
                    npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                });
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Refeição>().ToTable("Refeicoes");
        }
    }
}