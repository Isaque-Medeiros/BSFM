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

// --- ROTA 1: APENAS ENVIA O CÓDIGO (NÃO SALVA NO BANCO) ---
// O Front-end chama isso quando o usuário clica em "Cadastrar"
app.MapPost("/solicitar-codigo", (SolicitacaoEmail req) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();

    var email = req.Email.Trim().ToLower();
    
    // Verifica se esse usuário já existe no banco antes de mandar e-mail
    if (db.Usuarios.AsNoTracking().Any(u => u.Email.ToLower() == email))
        return Results.Json(new { mensagem = "Este e-mail já possui uma conta!" }, statusCode: 400);

    // Gera o código
    string token = new Random().Next(100000, 999999).ToString();
    
    // Dispara o email via API Mailtrap que configuramos
    EmailService.EnviarToken(email, token);

    // Retornamos o token para o seu JS poder conferir se o que o usuário digitou está certo
    return Results.Ok(new { mensagem = "Código enviado!", codigoParaValidar = token });
});

// --- ROTA 2: FINALMENTE CRIA O USUÁRIO NO BANCO ---
// O Front-end só chama isso quando o usuário digita o token certo no site
app.MapPost("/cadastrar-usuario-final", (Usuario usuarioVindoDoJs) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();

    var email = usuarioVindoDoJs.Email?.Trim().ToLower();

    // Proteção de última hora (BCrypt e Cálculos Nutricionais)
    usuarioVindoDoJs.Email = email;
    usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(usuarioVindoDoJs.SenhaHash);
    usuarioVindoDoJs.EmailVerificado = true; // Ele já confirmou o token no site
    
    new CalcularNutricional().RegistrarCalculos(usuarioVindoDoJs);

    db.Usuarios.Add(usuarioVindoDoJs);
    db.SaveChanges();

    return Results.Ok(new { mensagem = "Perfil criado com sucesso!" });
});

// --- ROTA DE LOGIN (MANTIDA) ---
app.MapPost("/login", (LoginDTO dadosLogin) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();

    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == dadosLogin.Email.Trim().ToLower());
    
    if (user != null && BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, user.SenhaHash))
    {
        return Results.Ok(new { 
            nome = user.Nome, 
            imc = user.IMC,
            tmb = user.TMB,
            gasto = user.GastoTotal,
            objetivo = user.TipoPessoa 
        }); 
    }
    
    return Results.Json(new { mensagem = "❌ E-mail ou senha incorretos." }, statusCode: 400);
});

app.Run();

// DTOs para comunicação
public record LoginDTO(string Email, string Senha);
public record SolicitacaoEmail(string Email);