using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ClassesBSFM;
using PonteBanco;
using System.Linq;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

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

app.MapGet("/", (IWebHostEnvironment env) => 
    Results.File(Path.Combine(env.ContentRootPath, "index.html"), "text/html"));

// --- ROTA DE REGISTRO (SOLICITA CÓDIGO) ---
app.MapPost("/solicitar-codigo", (SolicitacaoEmail req) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    var email = req.Email.Trim().ToLower();
    
    if (db.Usuarios.AsNoTracking().Any(u => u.Email.ToLower() == email))
        return Results.Json(new { mensagem = "E-mail já cadastrado!" }, statusCode: 400);

    string token = new Random().Next(100000, 999999).ToString();
    EmailService.EnviarToken(email, token);
    return Results.Ok(new { mensagem = "Código enviado!", tokenParaJs = token });
});

// --- ROTA DE CADASTRO FINAL (DEPOIS DO TOKEN) ---
app.MapPost("/cadastrar-usuario-final", (Usuario usuarioVindoDoJs) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(usuarioVindoDoJs.SenhaHash);
    usuarioVindoDoJs.EmailVerificado = true; 
    new CalcularNutricional().RegistrarCalculos(usuarioVindoDoJs);
    db.Usuarios.Add(usuarioVindoDoJs);
    db.SaveChanges();
    return Results.Ok(new { mensagem = "Perfil Criado!" });
});

// --- ROTA DE LOGIN ---
app.MapPost("/login", (LoginDTO dadosLogin) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == dadosLogin.Email.Trim().ToLower());
    if (user != null && BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, user.SenhaHash)) {
        return Results.Ok(new { nome = user.Nome, imc = user.IMC, tmb = user.TMB, gasto = user.GastoTotal }); 
    }
    return Results.Json(new { mensagem = "E-mail ou senha incorretos." }, statusCode: 400);
});

// --- ROTA DE RECUPERAÇÃO ---
app.MapPost("/esqueci-senha", (SolicitacaoEmail req) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == req.Email.ToLower());
    if (user == null) return Results.Json(new { mensagem = "E-mail não encontrado." }, statusCode: 404);
    
    string tokenRec = new Random().Next(100000, 999999).ToString();
    EmailService.EnviarToken(user.Email, tokenRec);
    return Results.Ok(new { mensagem = "Código Enviado!", tokenParaJs = tokenRec });
});

app.MapPost("/redefinir-senha", (RedefinicaoSenha req) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == req.Email.ToLower());
    if (user == null) return Results.NotFound();
    user.SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.NovaSenha);
    db.SaveChanges();
    return Results.Ok();
});

app.Run(); // FINAL DO ARQUIVO (Sempre assim!)

public record LoginDTO(string Email, string Senha);
public record SolicitacaoEmail(string Email);
public record RedefinicaoSenha(string Email, string NovaSenha);