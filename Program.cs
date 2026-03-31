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
using Microsoft.AspNetCore.Mvc;

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

builder.Services.AddHostedService<LimpezaAnalisesService>(); 
builder.Services.AddSingleton<BSFM.Services.YoloInferenceService>();
builder.Services.AddHttpClient<BSFM.Services.UsdaNutritionService>();
builder.Services.AddDbContext<BSFMContext>();

var app = builder.Build();
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<BSFMContext>();
    Console.WriteLine("[POSTGRES] Garantindo criação de tabelas...");
    db.Database.EnsureCreated(); // Isso vai criar 'analises_ia' forçadamente agora.
}

app.UseCors("PermitirSite");

// --- COMANDOS PARA O SITE FUNCIONAR ---
app.UseDefaultFiles(); // Faz o sistema procurar pelo index.html ou login.html automaticamente
app.UseStaticFiles();  // Importante: Entrega arquivos dentro da pasta 'wwwroot'

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
        return Results.Ok(new { id = user.ID, nome = user.Nome, imc = user.IMC, tmb = user.TMB, gasto = user.GastoTotal }); 
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
app.MapPost("/analisar-prato", async (
    [FromForm] IFormFile foto, 
    [FromForm] string porcao, 
    [FromForm] int usuarioId, 
    BSFM.Services.YoloInferenceService yolo, 
    BSFM.Services.UsdaNutritionService nutri, 
    PonteBanco.BSFMContext db) => 
{
    using var ms = new MemoryStream();
    await foto.CopyToAsync(ms);
    
    // 1. Chamar a NOVA função de Multi-detecção
    var alimentosEncontrados = yolo.DetectarAlimentos(ms.ToArray());

    if (alimentosEncontrados.Count == 0) 
        return Results.NotFound(new { mensagem = "Não identifiquei alimentos conhecidos no prato." });

    // Variáveis para acumular o valor total do prato
    double caloriasTotal = 0, protTotal = 0, carbTotal = 0, gordTotal = 0;

    // 2. Loop: Somar nutrição de todos os itens vistos pela IA
    foreach (var item in alimentosEncontrados)
    {
        var d = await nutri.BuscarNutrientes(item);
        if (d != null) 
        {
            double mult = porcao.ToLower() switch { "pequeno" => 1.5, "medio" => 3.0, "grande" => 5.0, _ => 3.0 };
            caloriasTotal += (d.Calorias100g * mult);
            protTotal += (d.Proteinas100g * mult);
            carbTotal += (d.Carbos100g * mult);
            gordTotal += (d.Gorduras100g * mult);
        }
        string traducaoPt = BSFM.Services.YoloInferenceService.Traducao.GetValueOrDefault(nomeEn, nomeEn);
        listaNomesPt.Add(traducaoPt);
    }

    // 3. Criar registro único do prato com a soma de tudo
    var analiseFinal = new ClassesBSFM.AnaliseIA {
        UsuarioID = usuarioId,
        Alimento = string.Join(", ", alimentosEncontrados), // Ex: "carrot, broccoli"
        Porcao = porcao,
        Calorias = Math.Round(caloriasTotal, 2),
        Proteinas = Math.Round(protTotal, 2),
        Carbos = Math.Round(carbTotal, 2),
        Gorduras = Math.Round(gordTotal, 2),
        DataAnalise = DateTime.Now
    };

    db.AnalisesIA.Add(analiseFinal);
    await db.SaveChangesAsync();

    return Results.Ok(new { dados = analiseFinal });
}).DisableAntiforgery();

app.Run(); // FINAL DO ARQUIVO

// Modelos de dados (DTOs)
public record LoginDTO(string Email, string Senha);
public record SolicitacaoEmail(string Email);
public record RedefinicaoSenha(string Email, string NovaSenha);
public record RedefinicaoFinal(string Email, string NovaSenha);
public record EsqueceuSenhaDTO(string Email);
public record RedefinicaoSenhaDTO(string Email, string NovaSenha);