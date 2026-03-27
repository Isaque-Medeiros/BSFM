using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ClassesBSFM;
using PonteBanco;
using System.Linq;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// PEGA A PORTA DO RAILWAY OU USA 8080 POR PADRÃO
var portVar = Environment.GetEnvironmentVariable("PORT") ?? "8080";
int port = int.Parse(portVar);

// FORÇA O KESTREL A OUVIR EM QUALQUER IP (0.0.0.0) E NA PORTA CORRETA
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(port);
});

builder.Services.AddCors(options => {
    options.AddPolicy("PermitirSite", policy => 
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();
app.UseCors("PermitirSite");

// TENTA CRIAR O BANCO LOGO DE CARA (Com aviso de erro no log)
try {
    using var scope = app.Services.CreateScope();
    using var db = new BSFMContext();
    db.Database.EnsureCreated();
    Console.WriteLine("[LOG] Banco de dados verificado/criado com sucesso.");
} catch (Exception ex) {
    Console.WriteLine($"[ERRO FATAL NO BANCO]: {ex.Message}");
}

// ROTA RAIZ (Onde o site carrega)
app.MapGet("/", () => 
{
    try {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "index.html");
        if (File.Exists(path)) {
            return Results.Content(File.ReadAllText(path), "text/html");
        } else {
            return Results.Content("<h1>Aviso</h1><p>index.html nao encontrado na pasta root.</p>", "text/html");
        }
    } catch (Exception ex) {
        return Results.Content($"<h1>Erro no Servidor</h1><p>{ex.Message}</p>", "text/html");
    }
});

app.MapPost("/cadastrar-usuario", (Usuario usuarioVindoDoJs) => {
    using var db = new BSFMContext();
    usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(usuarioVindoDoJs.SenhaHash);
    new CalcularNutricional().RegistrarCalculos(usuarioVindoDoJs);
    db.Usuarios.Add(usuarioVindoDoJs);
    db.SaveChanges();
    return Results.Ok(new { mensagem = "Sucesso!", id = usuarioVindoDoJs.ID });
});

app.MapPost("/login", (LoginDTO dadosLogin) => {
    using var db = new BSFMContext();
    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == dadosLogin.Email.Trim().ToLower());
    if (user != null && BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, user.SenhaHash))
        return Results.Ok(new { nome = user.Nome, imc = user.IMC });
    return Results.Json(new { mensagem = "Erro no login" }, statusCode: 400);
});

app.Run();

public record LoginDTO(string Email, string Senha);