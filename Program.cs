using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ClassesBSFM;
using PonteBanco;
using System.Linq;
using System.Net;

// 1. DATA FIX
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 2. PORTA DIRETA DO AMBIENTE (Forma mais estável para Nixpacks/Railway)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options =>
{
    // Ouvir em todas as interfaces na porta fornecida pela nuvem
    options.ListenAnyIP(int.Parse(port));
});

builder.Services.AddCors(options => {
    options.AddPolicy("PermitirSite", policy => 
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddDbContext<BSFMContext>();

var app = builder.Build();
app.UseCors("PermitirSite");

// 3. DATABASE (Igual ao seu anterior)
try {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("[LOG] Banco de dados pronto.");
} catch (Exception ex) {
    Console.WriteLine($"[AVISO BANCO]: {ex.Message}");
}

// 4. MONITORAMENTO E ROTA RAIZ
app.MapGet("/", async (context) => 
{
    Console.WriteLine($"[LOG] Health Check Recebido de: {context.Connection.RemoteIpAddress}");
    
    // Caminho prioritário na publicação .NET
    string path = Path.Combine(AppContext.BaseDirectory, "index.html");
    if (!File.Exists(path)) path = "index.html";

    if (File.Exists(path)) {
        await context.Response.WriteAsync(File.ReadAllText(path));
    } else {
        await context.Response.WriteAsync("<h1>BSFM ONLINE</h1><p>Aguardando primeiro cadastro.</p>");
    }
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
app.MapPost("/login", (LoginDTO dadosLogin) => 
{
    using (var db = new BSFMContext())
    {
        string emailProcurado = dadosLogin.Email.Trim().ToLower();
        var usuarioNoBanco = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == emailProcurado);

        if (usuarioNoBanco == null) 
            return Results.Json(new { mensagem = "E-mail não encontrado." }, statusCode: 400);

        bool senhaCorreta = BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, usuarioNoBanco.SenhaHash);

        if (senhaCorreta)
        {
            // Retornamos um objeto completo para o Front-end salvar
            return Results.Ok(new { 
                id = usuarioNoBanco.ID,
                nome = usuarioNoBanco.Nome,
                email = usuarioNoBanco.Email,
                imc = usuarioNoBanco.IMC,
                tmb = usuarioNoBanco.TMB,
                gastoTotal = usuarioNoBanco.GastoTotal,
                peso = usuarioNoBanco.Peso,
                altura = usuarioNoBanco.Altura,
                tipoPessoa = usuarioNoBanco.TipoPessoa
            });
        }
        return Results.Json(new { mensagem = "Senha incorreta." }, statusCode: 400);
    }
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