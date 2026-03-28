using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ClassesBSFM;
using PonteBanco;
using System.Linq;

// 1. DATA FIX para o PostgreSQL do Railway
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 2. Configuração de Porta Dinâmica do Railway
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(port));
});

// Serviços
builder.Services.AddCors(options => {
    options.AddPolicy("PermitirSite", policy => 
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddDbContext<BSFMContext>();

var app = builder.Build();

// 3. Configuração de Arquivos Estáticos (Como os seus HTMLs estão na RAIZ)
app.UseDefaultFiles(); 
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        app.Environment.ContentRootPath),
    RequestPath = ""
});

app.UseCors("PermitirSite");

// 4. BANCO DE DADOS (Verificação Inicial)
try {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    db.Database.EnsureCreated();
    Console.WriteLine("[LOG] Banco de dados pronto.");
} catch (Exception ex) {
    Console.WriteLine($"[AVISO BANCO]: {ex.Message}");
}

// 5. ROTA RAIZ (UNIFICADA - Entrega o index.html e serve de Health Check)
app.MapGet("/", async (context) => 
{
    string path = Path.Combine(app.Environment.ContentRootPath, "index.html");
    if (File.Exists(path)) {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(path);
    } else {
        await context.Response.WriteAsync("<h1>BSFM ONLINE</h1><p>Servidor ativo, index.html não encontrado na raiz.</p>");
    }
});

// 6. ROTA DE CADASTRO (Com verificação de E-mail)
app.MapPost("/cadastrar-usuario", (Usuario usuarioVindoDoJs) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    
    string emailTratado = usuarioVindoDoJs.Email.Trim().ToLower();

    // Bloqueia e-mail duplicado
    bool jaExiste = db.Usuarios.Any(u => u.Email.ToLower() == emailTratado);
    if (jaExiste) {
        return Results.Json(new { mensagem = "Este e-mail já está cadastrado!" }, statusCode: 400);
    }

    usuarioVindoDoJs.Email = emailTratado;
    usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(usuarioVindoDoJs.SenhaHash);
    new CalcularNutricional().RegistrarCalculos(usuarioVindoDoJs);
    
    db.Usuarios.Add(usuarioVindoDoJs);
    db.SaveChanges();
    
    return Results.Ok(new { mensagem = "Conta criada com sucesso!" });
});

// 7. ROTA DE LOGIN
app.MapPost("/login", (LoginDTO dadosLogin) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();

    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == dadosLogin.Email.Trim().ToLower());
    
    if (user != null && BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, user.SenhaHash)) {
        return Results.Ok(new { 
            nome = user.Nome, 
            imc = user.IMC,
            tmb = user.TMB,
            gasto = user.GastoTotal,
            objetivo = user.TipoPessoa 
        }); 
    }
    
    return Results.Json(new { mensagem = "E-mail ou senha incorretos." }, statusCode: 400);
});

app.Run();

// Objeto para receber dados do login
public record LoginDTO(string Email, string Senha);