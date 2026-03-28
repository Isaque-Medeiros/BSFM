using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ClassesBSFM;
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

// CADASTRO COM TRAVA DE EMAIL
app.MapPost("/cadastrar-usuario", (Usuario usuarioVindoDoJs) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    
    var email = usuarioVindoDoJs.Email?.Trim().ToLower();
    if (db.Usuarios.Any(u => u.Email.ToLower() == email))
        return Results.Json(new { mensagem = "Este e-mail já existe!" }, statusCode: 400);

    // Gerar Token e salvar usuário desativado
    string token = new Random().Next(100000, 999999).ToString();
    usuarioVindoDoJs.TokenVerificacao = token;
    usuarioVindoDoJs.EmailVerificado = false; // IMPORTANTE: Bloqueado
    
    usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(usuarioVindoDoJs.SenhaHash);
    new CalcularNutricional().RegistrarCalculos(usuarioVindoDoJs);
    
    db.Usuarios.Add(usuarioVindoDoJs);
    db.SaveChanges();

    // Enviar e-mail em paralelo
    Task.Run(() => EmailService.EnviarToken(email, token));

    return Results.Ok(new { mensagem = "Cadastro pré-realizado! Verifique seu e-mail." });
});

// --- NOVA ROTA: VERIFICAR TOKEN ---
app.MapPost("/verificar-token", (TokenRequest req) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();

    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == req.Email.ToLower() && u.TokenVerificacao == req.Token);

    if (user == null)
        return Results.Json(new { mensagem = "Código incorreto ou expirado." }, statusCode: 400);

    user.EmailVerificado = true; // ATIVA A CONTA
    user.TokenVerificacao = null; // Limpa o token por segurança
    db.SaveChanges();

    return Results.Ok(new { mensagem = "Conta ativada com sucesso!" });
});

// --- ROTA DE LOGIN (Atualizada para checar se está verificado) ---
app.MapPost("/login", (LoginDTO dadosLogin) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == dadosLogin.Email.Trim().ToLower());
    
    if (user != null && BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, user.SenhaHash)) {
        if (!user.EmailVerificado)
            return Results.Json(new { mensagem = "Aguardando confirmação de e-mail." }, statusCode: 401);

        return Results.Ok(new { nome = user.Nome, imc = user.IMC, tmb = user.TMB, gasto = user.GastoTotal });
    }
    
    return Results.Json(new { mensagem = "E-mail ou senha incorretos." }, statusCode: 400);
});

app.Run();

public record LoginDTO(string Email, string Senha);
public record TokenRequest(string Email, string Token);