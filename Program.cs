using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using ClassesBSFM;
using PonteBanco;
using System.Linq;
using BSFM.Services; 
using Microsoft.AspNetCore.Http;
using System.IO;

// Correção para trabalhar com datas no PostgreSQL (comum no Railway)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Configuração da Porta para o Railway
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Configurações de Serviços
builder.Services.AddCors(options => {
    options.AddPolicy("PermitirSite", policy => 
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddSingleton<BSFM.Services.YoloInferenceService>();
builder.Services.AddHttpClient<BSFM.Services.UsdaNutritionService>();
builder.Services.AddDbContext<BSFMContext>();

var app = builder.Build();

app.UseCors("PermitirSite");

// --- COMANDOS PARA O SITE FUNCIONAR ---
app.UseDefaultFiles(); // Faz o sistema procurar pelo index.html ou login.html automaticamente
app.UseStaticFiles();  // Importante: Entrega arquivos dentro da pasta 'wwwroot'

// Inicializar o Banco de Dados com Segurança
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    db.Database.EnsureCreated();
}

// Rota para a Página Inicial (Fallback caso o DefaultFiles não pegue)
app.MapGet("/", (IWebHostEnvironment env) => 
    Results.File(Path.Combine(env.WebRootPath ?? "wwwroot", "index.html"), "text/html"));

// --- SUAS ROTAS DE API ---

app.MapPost("/solicitar-codigo", (SolicitacaoEmail req) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    var email = req.Email.Trim().ToLower();
    
    if (db.Usuarios.AsNoTracking().Any(u => u.Email.ToLower() == email))
        return Results.Json(new { mensagem = "E-mail já cadastrado!" }, statusCode: 400);

    // 2. Gera o Token de 6 dígitos
    string token = new Random().Next(100000, 999999).ToString();

    // 3. CHAMA O SERVIÇO DE E-MAIL (Aqui ele envia para o Mailtrap)
    EmailService.EnviarToken(email, token);
    return Results.Ok(new { mensagem = "Código enviado!", tokenParaJs = token });
});

app.MapPost("/cadastrar-usuario-final", (Usuario usuarioVindoDoJs) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    // Cuidado: Certifique-se que o pacote BCrypt.Net-Next está no .csproj
    usuarioVindoDoJs.SenhaHash = BCrypt.Net.BCrypt.HashPassword(usuarioVindoDoJs.SenhaHash);
    usuarioVindoDoJs.EmailVerificado = true; 
    new CalcularNutricional().RegistrarCalculos(usuarioVindoDoJs);
    db.Usuarios.Add(usuarioVindoDoJs);
    db.SaveChanges();
    return Results.Ok(new { mensagem = "Perfil Criado!" });
});

app.MapPost("/login", (LoginDTO dadosLogin) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == dadosLogin.Email.Trim().ToLower());
    if (user != null && BCrypt.Net.BCrypt.Verify(dadosLogin.Senha, user.SenhaHash)) {
        return Results.Ok(new { nome = user.Nome, imc = user.IMC, tmb = user.TMB, gasto = user.GastoTotal }); 
    }
    return Results.Json(new { mensagem = "E-mail ou senha incorretos." }, statusCode: 400);
});

// --- ROTA: ESQUECI MINHA SENHA (PASSO 1) ---
app.MapPost("/esqueci-senha", (EsqueceuSenhaDTO req) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    var email = req.Email.Trim().ToLower();

    // 1. Verifica se o usuário existe
    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == email);
    if (user == null)
        return Results.Json(new { mensagem = "E-mail não encontrado em nossa base." }, statusCode: 404);

    // 2. Gera o Token de 6 dígitos
    string token = new Random().Next(100000, 999999).ToString();

    // 3. CHAMA O SERVIÇO DE E-MAIL (Aqui ele envia para o Mailtrap)
    EmailService.EnviarToken(email, token);

    // 4. Retorna para o JS para que ele possa comparar o token depois
    return Results.Ok(new { mensagem = "Código enviado com sucesso!", tokenParaJs = token });
});

// --- ROTA: REDEFINIR SENHA (PASSO 2 - FINAL) ---
app.MapPost("/redefinir-senha", (RedefinicaoSenhaDTO req) => {
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    var email = req.Email.Trim().ToLower();

    // 1. Localiza o usuário
    var user = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == email);
    if (user == null)
        return Results.Json(new { mensagem = "Usuário não identificado." }, statusCode: 404);

    // 2. Criptografa a nova senha e salva
    user.SenhaHash = BCrypt.Net.BCrypt.HashPassword(req.NovaSenha);
    
    db.Usuarios.Update(user);
    db.SaveChanges();

    return Results.Ok(new { mensagem = "Senha atualizada com sucesso!" });
});
// Outras rotas permanecem...
app.MapPost("/analisar-prato", async ([FromForm] IFormFile foto, [FromForm] string porcao, BSFM.Services.YoloInferenceService yolo, BSFM.Services.UsdaNutritionService nutri) => 
{
    if (foto == null || foto.Length == 0) return Results.BadRequest("Nenhuma imagem enviada.");

    // Validação de Integridade: Segurança (CIA - Integrity)
    if (foto.Length > 5 * 1024 * 1024) return Results.BadRequest("Imagem muito grande (Max 5MB)");

    // 1. Receber bytes
    using var ms = new MemoryStream();
    await foto.CopyToAsync(ms);
    var imagemBytes = ms.ToArray();

    // 2. IA LOCAL - Detecção (Latência quase zero)
    var labelIngles = yolo.DetectarAlimento(imagemBytes);
    if (labelIngles == "unknown") return Results.NotFound(new { mensagem = "Não consegui identificar nenhum alimento no prato." });

    // 3. API USDA - Nutrientes por 100g
    var dadosNutricionais = await nutri.BuscarNutrientes(labelIngles);
    if (dadosNutricionais == null) return Results.NotFound(new { mensagem = "Nutrientes não encontrados para: " + labelIngles });

    // 4. Regra de Negócio: Mapeamento de Porção (Calculo Nutricional)
    double multiplicador = porcao.ToLower() switch {
        "pequeno" => 1.5,   // ~150g
        "medio" => 3.0,     // ~300g
        "grande" => 5.0,    // ~500g
        _ => double.TryParse(porcao, out var g) ? g / 100.0 : 3.0
    };

    // 5. Retorno JSON Final
    return Results.Ok(new {
        AlimentoDetectado = labelIngles,
        PorcaoReferencia = porcao,
        CalculadoParaGramas = multiplicador * 100,
        CaloriasTotais = Math.Round(dadosNutricionais.Calorias100g * multiplicador, 2),
        Proteinas = Math.Round(dadosNutricionais.Proteinas100g * multiplicador, 2),
        Carboidratos = Math.Round(dadosNutricionais.Carbos100g * multiplicador, 2),
        Gorduras = Math.Round(dadosNutricionais.Gorduras100g * multiplicador, 2)
    });
});

app.Run(); // FINAL DO ARQUIVO

// Modelos de dados (DTOs)
public record LoginDTO(string Email, string Senha);
public record SolicitacaoEmail(string Email);
public record RedefinicaoSenha(string Email, string NovaSenha);
public record RedefinicaoFinal(string Email, string NovaSenha);
public record EsqueceuSenhaDTO(string Email);
public record RedefinicaoSenhaDTO(string Email, string NovaSenha);