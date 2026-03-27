using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ClassesBSFM;
using PonteBanco;
using System.Linq;
using System.Net;
using System;
using System.IO;

// 1. SEMPRE A PRIMEIRA LINHA (Configuração de datas para o Postgres)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 2. CONFIGURAÇÃO DA PORTA E DO SERVIDOR KESTREL
var portVar = Environment.GetEnvironmentVariable("PORT") ?? "8080";
int port = int.Parse(portVar);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(IPAddress.Any, port);
});

// 3. ADICIONAR SERVIÇOS (CORS E BANCO DE DADOS)
builder.Services.AddCors(options => {
    options.AddPolicy("PermitirSite", policy => 
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// AVISO AO C# COMO CONECTAR AO BANCO (Crucial para funcionar o GetRequiredService abaixo)
builder.Services.AddDbContext<BSFMContext>();

var app = builder.Build();

// 4. CONFIGURAÇÃO DE MIDDLEWARE
app.UseCors("PermitirSite");

// 5. TENTA CRIAR AS TABELAS NO POSTGRES ASSIM QUE LIGA
try {
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
        db.Database.EnsureCreated();
        Console.WriteLine("[LOG] Conexao com banco verificada e tabelas prontas.");
    }
} catch (Exception ex) {
    Console.WriteLine($"[ERRO FATAL NO BANCO]: {ex.Message}");
    // Não paramos o app aqui para o Railway não entrar em loop, mas o erro aparecerá no log
}

// 6. ROTA RAIZ (Onde o seu site carrega no navegador)
app.MapGet("/", () => 
{
    string[] locaisPossiveis = {
        Path.Combine(AppContext.BaseDirectory, "index.html"),
        Path.Combine(Directory.GetCurrentDirectory(), "index.html"),
        "/app/index.html",
        "/app/out/index.html"
    };

    foreach (var path in locaisPossiveis)
    {
        if (File.Exists(path))
        {
            return Results.Content(File.ReadAllText(path), "text/html");
        }
    }

    return Results.Content($"<h1>API BSFM Online</h1><p>index.html nao encontrado.</p><small>Rodando em: {Directory.GetCurrentDirectory()}</small>", "text/html");
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