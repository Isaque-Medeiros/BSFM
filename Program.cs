using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ClassesBSFM;
using PonteBanco;
using System.Linq;
using System.Net;

// 1. DATA FIX
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 2. PORTA DIRETA DO AMBIENTE (Forma mais estável para Nixpacks/Railway)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options =>
{
    // Ouvir em todas as interfaces na porta fornecida pela nuvem
    options.ListenAnyIP(int.Parse(port));
});

builder.Services.AddCors(options => {
    options.AddPolicy("PermitirSite", policy => 
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddDbContext<BSFMContext>();

var app = builder.Build();

app.UseDefaultFiles(); // Procura por index.html automaticamente
app.UseStaticFiles();  // Permite servir dashboard.html, CSS, etc.

app.UseCors("PermitirSite");

// 3. DATABASE (Igual ao seu anterior)
try {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("[LOG] Banco de dados pronto.");
} catch (Exception ex) {
    Console.WriteLine($"[AVISO BANCO]: {ex.Message}");
}

// 4. MONITORAMENTO E ROTA RAIZ
app.MapGet("/", async (context) => 
{
    Console.WriteLine($"[LOG] Health Check Recebido de: {context.Connection.RemoteIpAddress}");
    
    // Caminho prioritário na publicação .NET
    string path = Path.Combine(AppContext.BaseDirectory, "index.html");
    if (!File.Exists(path)) path = "index.html";

    if (File.Exists(path)) {
        await context.Response.WriteAsync(File.ReadAllText(path));
    } else {
        await context.Response.WriteAsync("<h1>BSFM ONLINE</h1><p>Aguardando primeiro cadastro.</p>");
    }
});

// 7. ROTA DE CADASTRO
app.MapPost("/cadastrar-usuario", (Usuario usuarioVindoDoJs) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    
    // Processa senha e cálculos
    usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(usuarioVindoDoJs.SenhaHash);
    new CalcularNutricional().RegistrarCalculos(usuarioVindoDoJs);
    
    db.Usuarios.Add(usuarioVindoDoJs);
    db.SaveChanges();
    
    return Results.Ok(new { mensagem = "Sucesso!", id = usuarioVindoDoJs.ID });
});

// 8. ROTA DE LOGIN
app.MapPost("/login", (LoginDTO dadosLogin) => {
    using var scope = app.Services.CreateScope(); // MANTIDO: Essencial para estabilidade
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();

    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == dadosLogin.Email.Trim().ToLower());
    
    if (user != null && BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, user.SenhaHash))
    {
        // Retornamos um pacote completo de informações para o Front-end
        return Results.Ok(new { 
            nome = user.Nome, 
            imc = user.IMC,
            tmb = user.TMB,
            gasto = user.GastoTotal,
            objetivo = user.TipoPessoa 
        }); 
    }
    
    return Results.Json(new { mensagem = "E-mail ou senha incorretos." }, statusCode: 400);
});

// 9. ROTA DE DEBUG (Para ver se os usuários estão lá)
app.MapGet("/debug-usuarios", () => 
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    return Results.Ok(db.Usuarios.ToList());
});

app.Run();

// Objeto para receber dados do login
public record LoginDTO(string Email, string Senha);