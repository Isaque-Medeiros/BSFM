using Microsoft.EntityFrameworkCore;
using ClassesBSFM;
using System;

namespace PonteBanco
{
    public class BSFMContext : DbContext
    {
        // Tabelas do Banco
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Refeição> Refeicoes { get; set; }
        public DbSet<Comida> Comidas { get; set; }
        public DbSet<CronogramaAlimentar> Cronogramas { get; set; }
        public DbSet<Hospital> Hospitais { get; set; }
        public DbSet<AnaliseIA> AnalisesIA { get; set; } // ADICIONADO: Nova tabela

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var connectionUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            if (string.IsNullOrEmpty(connectionUrl))
            {
                // Se rodar local sem a variável, usa SQLite
                options.UseSqlite("Data Source=UsuariosBSFM.db");
            }
            else
            {
                // Tenta extrair os dados da URL do Railway
                try 
                {
                    var databaseUri = new Uri(connectionUrl);
                    var userInfo = databaseUri.UserInfo.Split(':');

                    var connectionString = $"Host={databaseUri.Host};" +
                                           $"Port={databaseUri.Port};" +
                                           $"Username={userInfo[0]};" +
                                           $"Password={userInfo[1]};" +
                                           $"Database={databaseUri.LocalPath.TrimStart('/')};" +
                                           "SSL Mode=Require;" +
                                           "Trust Server Certificate=true;" +
                                           "Pooling=true;";

                    options.UseNpgsql(connectionString);
                }
                catch 
                {
                    // Plano de reserva se a URL estiver em formato simples
                    var fallbackString = connectionUrl.Replace("postgres://", "postgresql://");
                    options.UseNpgsql(fallbackString);
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Ajuste para tabelas com nomes especiais
            modelBuilder.Entity<Refeição>().ToTable("Refeicoes");
            modelBuilder.Entity<AnaliseIA>().ToTable("analises_ia");
        }
    }
}