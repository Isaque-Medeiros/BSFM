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

    string token = new Random().Next(100000, 999999).ToString();
    usuarioVindoDoJs.TokenVerificacao = token;
    usuarioVindoDoJs.EmailVerificado = false; // Fica travado aqui!

    usuarioVindoDoJs.Email = email;
    usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(usuarioVindoDoJs.SenhaHash);
    new CalcularNutricional().RegistrarCalculos(usuarioVindoDoJs);
    
    db.Usuarios.Add(usuarioVindoDoJs);
    db.SaveChanges();

    // Envia o e-mail em background
    _ = Task.Run(() => EmailService.EnviarToken(email, token));

    return Results.Ok(new { mensagem = "Verifique seu e-mail para ativar a conta." });
});

// --- NOVA ROTA: VERIFICAR TOKEN ---
app.MapPost("/verificar-token", (TokenRequest req) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();

    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == req.Email.ToLower());

    if (user != null && user.TokenVerificacao == req.Token) {
        user.EmailVerificado = true; // AGORA A CONTA ESTÁ ATIVA
        user.TokenVerificacao = null; 
        db.SaveChanges();
        return Results.Ok(new { mensagem = "Conta ativada com sucesso!" });
    }
    
    return Results.Json(new { mensagem = "Código inválido." }, statusCode: 400);
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