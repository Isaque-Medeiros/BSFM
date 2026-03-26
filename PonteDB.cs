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
            // O caminho do banco de dados
            options.UseSqlite("Data Source=UsuariosBSFM.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Isso evita erros caso o banco tente criar uma tabela com acento no nome físico
            modelBuilder.Entity<Refeição>().ToTable("Refeicoes");
        }
    }
}