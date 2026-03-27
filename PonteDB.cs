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
            // Parsing manual da URL do Railway para evitar erro de formato
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
    }
}