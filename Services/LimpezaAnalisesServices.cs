using Microsoft.Extensions.Hosting;
using PonteBanco;
using Microsoft.EntityFrameworkCore;

public class LimpezaAnalisesService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public LimpezaAnalisesService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
                
                // Pega a data de 2 dias atrás
                var limite = DateTime.Now.AddDays(-2);

                // Localiza análises expiradas
                var expiradas = await db.AnalisesIA
                    .Where(a => a.DataAnalise < limite)
                    .ToListAsync();

                if (expiradas.Any())
                {
                    db.AnalisesIA.RemoveRange(expiradas);
                    await db.SaveChangesAsync();
                    Console.WriteLine($"[LIMPEZA] {expiradas.Count} análises antigas removidas.");
                }
            }
            // Aguarda 1 hora antes de verificar de novo
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}