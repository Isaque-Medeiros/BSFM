using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ClassesBSFM;
using PonteBanco;
using System.Linq; // Necessário para o FirstOrDefault

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// 1. Configuração de Serviços
builder.Services.AddCors(options => {
    options.AddPolicy("PermitirSite", policy => 
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// 2. Configuração de Middleware
app.UseCors("PermitirSite");

// --- ROTA DE CADASTRO ---
app.MapPost("/cadastrar-usuario", (Usuario usuarioVindoDoJs) => 
{
    using (var db = new BSFMContext())
    {
        db.Database.EnsureCreated();

        // Criptografia da senha
        string senhaPura = usuarioVindoDoJs.SenhaHash;
        usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(senhaPura);

        // Cálculos nutricionais
        CalcularNutricional calc = new CalcularNutricional();
        calc.RegistrarCalculos(usuarioVindoDoJs);

        db.Usuarios.Add(usuarioVindoDoJs);
        db.SaveChanges();

        return Results.Ok(new { 
            mensagem = "Usuário cadastrado com sucesso!", 
            id = usuarioVindoDoJs.ID 
        });
    }
});

// --- ROTA DE LOGIN (Movida para antes do app.Run) ---
app.MapPost("/login", (LoginDTO dadosLogin) => 
{
    using (var db = new BSFMContext())
    {
        // Tratamento simples para evitar erros de espaço ou maiúsculas
        string emailProcurado = dadosLogin.Email.Trim().ToLower();

        var usuarioNoBanco = db.Usuarios
            .FirstOrDefault(u => u.Email.ToLower() == emailProcurado);

        if (usuarioNoBanco == null) 
            return Results.Json(new { mensagem = "E-mail não encontrado." }, statusCode: 400);

        // Verifica a senha
        bool senhaCorreta = BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, usuarioNoBanco.SenhaHash);

        if (senhaCorreta)
        {
            return Results.Ok(new { 
                mensagem = "Login realizado!", 
                nome = usuarioNoBanco.Nome,
                imc = usuarioNoBanco.IMC 
            });
        }
        else
        {
            return Results.Json(new { mensagem = "Senha incorreta." }, statusCode: 400);
        }
    }
});
app.MapGet("/debug-usuarios", () => 
{
    using (var db = new BSFMContext())
    {
        // Retorna todos os usuários cadastrados no arquivo SQLite atual
        return Results.Ok(db.Usuarios.ToList());
    }
});

app.MapGet("/", () => Results.Content(File.ReadAllText("index.html"), "text/html"));

app.Run();

public record LoginDTO(string Email, string Senha);
