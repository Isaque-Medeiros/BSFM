using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using ClassesBSFM;
using PonteBanco;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// AJUSTE 1: Prioridade total para a porta que o Railway mandar
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080"; 
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddCors(options => {
    options.AddPolicy("PermitirSite", policy => 
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();
app.UseCors("PermitirSite");

// --- ROTA DE CADASTRO (Mantida igual) ---
app.MapPost("/cadastrar-usuario", (Usuario usuarioVindoDoJs) => 
{
    using (var db = new BSFMContext())
    {
        db.Database.EnsureCreated();
        string senhaPura = usuarioVindoDoJs.SenhaHash;
        usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(senhaPura);
        CalcularNutricional calc = new CalcularNutricional();
        calc.RegistrarCalculos(usuarioVindoDoJs);
        db.Usuarios.Add(usuarioVindoDoJs);
        db.SaveChanges();
        return Results.Ok(new { mensagem = "Usuário cadastrado com sucesso!", id = usuarioVindoDoJs.ID });
    }
});

// --- ROTA DE LOGIN (Mantida igual) ---
app.MapPost("/login", (LoginDTO dadosLogin) => 
{
    using (var db = new BSFMContext())
    {
        string emailProcurado = dadosLogin.Email.Trim().ToLower();
        var usuarioNoBanco = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == emailProcurado);
        if (usuarioNoBanco == null) return Results.Json(new { mensagem = "E-mail não encontrado." }, statusCode: 400);
        
        bool senhaCorreta = BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, usuarioNoBanco.SenhaHash);
        if (senhaCorreta) return Results.Ok(new { mensagem = "Login realizado!", nome = usuarioNoBanco.Nome, imc = usuarioNoBanco.IMC });
        return Results.Json(new { mensagem = "Senha incorreta." }, statusCode: 400);
    }
});

app.MapGet("/debug-usuarios", () => 
{
    using (var db = new BSFMContext())
    {
        return Results.Ok(db.Usuarios.ToList());
    }
});

// --- AJUSTE 2: ROTA RAIZ SEGURA (O Health Check do Railway) ---
app.MapGet("/", () => 
{
    // Tentamos ler o arquivo. Se falhar, mandamos um texto em vez de travar o servidor
    try {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "index.html");
        return Results.Content(File.ReadAllText(path), "text/html");
    } catch {
        return Results.Content("<h1>API BSFM Online</h1><p>Mas o arquivo index.html nao foi encontrado no servidor.</p>", "text/html");
    }
});

app.Run();

public record LoginDTO(string Email, string Senha);