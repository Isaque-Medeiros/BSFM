using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ClassesBSFM; // ESTA LINHA É OBRIGATÓRIA
using PonteBanco;
using System.Linq;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// PEGA A PORTA DO RAILWAY DE FORMA LIMPA
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddCors(options => {
    options.AddPolicy("PermitirSite", policy => 
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddDbContext<BSFMContext>();

var app = builder.Build();

app.UseCors("PermitirSite");
app.UseDefaultFiles();
app.UseStaticFiles();

// DATABASE
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();

    db.Database.EnsureCreated();
}

// ROTA RAIZ SEGURA
app.MapGet("/", (IWebHostEnvironment env) => 
    Results.File(Path.Combine(env.ContentRootPath, "index.html"), "text/html"));

// ROTA DE CADASTRO
app.MapPost("/cadastrar-usuario", (Usuario usuarioVindoDoJs) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    
    var email = usuarioVindoDoJs.Email?.Trim().ToLower();
    if (db.Usuarios.Any(u => u.Email.ToLower() == email))
        return Results.Json(new { mensagem = "Este e-mail já existe!" }, statusCode: 400);

    // Gerar código aleatório
    string token = new Random().Next(100000, 999999).ToString();
    
    // Configura o usuário mas deixa BLOQUEADO
    usuarioVindoDoJs.TokenVerificacao = token;
    usuarioVindoDoJs.EmailVerificado = false; 
    usuarioVindoDoJs.Email = email;
    usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(usuarioVindoDoJs.SenhaHash);
    new CalcularNutricional().RegistrarCalculos(usuarioVindoDoJs);
    
    db.Usuarios.Add(usuarioVindoDoJs);
    db.SaveChanges();

    // DISPARA O EMAIL (Mailtrap não dá Timeout como o Gmail)
    _ = Task.Run(() => EmailService.EnviarToken(email, token));

    return Results.Ok(new { mensagem = "Usuário pré-cadastrado! Verifique seu e-mail no Mailtrap." });
});


// NOVA ROTA: ATIVAR CONTA VIA TOKEN
app.MapPost("/verificar-token", (TokenRequest req) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();

    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == req.Email.ToLower());

    if (user != null && user.TokenVerificacao == req.Token) {
        user.EmailVerificado = true; // CONTA ATIVA!
        user.TokenVerificacao = null; 
        db.SaveChanges();
        return Results.Ok(new { mensagem = "Sua conta foi ativada com sucesso! Faça login." });
    }
    
    return Results.Json(new { mensagem = "Código de ativação inválido." }, statusCode: 400);
});

app.MapPost("/login", (LoginDTO dadosLogin) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();

    // 1. Busca o usuário pelo e-mail
    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == dadosLogin.Email.Trim().ToLower());
    
    // 2. Verifica se o usuário existe e se a senha (BCrypt) bate
    if (user != null && BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, user.SenhaHash))
    {
        // --- A NOVA TRAVA DE SEGURANÇA ---
        if (!user.EmailVerificado)
        {
            // Retorna Status 401 (Não autorizado) informando que falta o token
            return Results.Json(new { mensagem = "⚠️ Sua conta ainda não foi ativada. Verifique o código no seu e-mail (Mailtrap)." }, statusCode: 401);
        }

        // 3. LOGIN COM SUCESSO: Envia os dados para o dashboard
        return Results.Ok(new { 
            nome = user.Nome, 
            imc = user.IMC,
            tmb = user.TMB,
            gasto = user.GastoTotal,
            objetivo = user.TipoPessoa 
        }); 
    }
    
    // Erro de e-mail ou senha
    return Results.Json(new { mensagem = "❌ E-mail ou senha incorretos." }, statusCode: 400);
});
app.Run();

public record LoginDTO(string Email, string Senha);
public record TokenRequest(string Email, string Token);