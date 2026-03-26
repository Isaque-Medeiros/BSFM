using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ClassesBSFM;
using PonteBanco;
using System.Linq; // Necessário para o FirstOrDefault

var builder = WebApplication.CreateBuilder(args);

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

// 3. Classe DTO e Inicialização (SEMPRE NO FINAL)
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");

public record LoginDTO(string Email, string Senha);