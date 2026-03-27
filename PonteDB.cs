using Microsoft.EntityFrameworkCore;
using ClassesBSFM;

namespace PonteBanco
{
    public class BSFMContext : DbContext
    {
        // Aqui definimos quais classes o C# deve transformar em Tabelas no Banco
        public DbSet<Usuario> Usuarios { get; set; }
        
        // Adicionei estas para que o SQLite crie as tabelas de suporte que você já codificou
        public DbSet<Refeição> Refeicoes { get; set; }
        public DbSet<Comida> Comidas { get; set; }
        public DbSet<CronogramaAlimentar> Cronogramas { get; set; }
        public DbSet<Hospital> Hospitais { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // O Railway fornece essa variável automaticamente
            string? connectionUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            if (string.IsNullOrEmpty(connectionUrl))
            {
                // Se estiver rodando no seu PC sem internet/config, usa SQLite
                options.UseSqlite("Data Source=UsuariosBSFM.db");
            }
            else
            {
                // AJUSTE PARA O RAILWAY: 
                // O Railway entrega a URL no formato postgres://... 
                // O Npgsql precisa converter esse formato para uma String de Conexão.
                var databaseUri = new Uri(connectionUrl);
                var userInfo = databaseUri.UserInfo.Split(':');

                var connectionString = $"Host={databaseUri.Host};" +
                                    $"Port={databaseUri.Port};" +
                                    $"Username={userInfo[0]};" +
                                    $"Password={userInfo[1]};" +
                                    $"Database={databaseUri.LocalPath.TrimStart('/')};" +
                                    "SSL Mode=Require;Trust Server Certificate=true;";

                options.UseNpgsql(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Isso evita erros caso o banco tente criar uma tabela com acento no nome físico
            modelBuilder.Entity<Refeição>().ToTable("Refeicoes");
        }
    }
}