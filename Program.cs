using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ClassesBSFM;
using PonteBanco;
using System.Linq;
using System.Net;

// 1. DATABASE COMPATIBILITY
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 2. PORTA (REMOVI O CONFIGUREKESTREL MANUAL)
// Deixamos o .NET gerenciar a porta automaticamente via variáveis do Railway
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddCors(options => {
    options.AddPolicy("PermitirSite", policy => 
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddDbContext<BSFMContext>();

var app = builder.Build();
app.UseCors("PermitirSite");

// 3. DATABASE INIT (Simplificado)
try {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("[LOG] Banco de dados pronto.");
} catch (Exception ex) {
    Console.WriteLine($"[AVISO BANCO]: {ex.Message}");
}

// 4. ROTA RAIZ (Com Log de Monitoramento)
app.MapGet("/", (HttpContext context) => 
{
    Console.WriteLine($"[LOG] Recebi uma visita de: {context.Connection.RemoteIpAddress}");
    
    string[] locais = {
        Path.Combine(AppContext.BaseDirectory, "index.html"),
        Path.Combine(Directory.GetCurrentDirectory(), "index.html"),
        "index.html"
    };

    foreach (var p in locais) {
        if (File.Exists(p)) return Results.Content(File.ReadAllText(p), "text/html");
    }

    return Results.Content("<h1>BSFM Online</h1>", "text/html");
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
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();

    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == dadosLogin.Email.Trim().ToLower());
    
    if (user != null && BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, user.SenhaHash))
    {
        return Results.Ok(new { nome = user.Nome, imc = user.IMC });
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