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

    usuarioVindoDoJs.Email = email;
    usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(usuarioVindoDoJs.SenhaHash);
    new CalcularNutricional().RegistrarCalculos(usuarioVindoDoJs);

    string tokenGerado = new Random().Next(100000, 999999).ToString();
    usuarioVindoDoJs.TokenVerificacao = tokenGerado;
    usuarioVindoDoJs.EmailVerificado = false;

    // ENVIA O E-MAIL
    
    db.Usuarios.Add(usuarioVindoDoJs);
    db.SaveChanges();
    
    Task.Run(() => EmailService.EnviarToken(usuarioVindoDoJs.Email, tokenGerado));
    return Results.Ok(new { mensagem = "Conta criada com sucesso!" });
});

// LOGIN
app.MapPost("/login", (LoginDTO dadosLogin) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == dadosLogin.Email.Trim().ToLower());
    
    if (user != null && BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, user.SenhaHash))
        return Results.Ok(new { nome = user.Nome, imc = user.IMC, tmb = user.TMB, gasto = user.GastoTotal, objetivo = user.TipoPessoa });
    
    return Results.Json(new { mensagem = "E-mail ou senha incorretos." }, statusCode: 400);
});

app.Run();

public record LoginDTO(string Email, string Senha);