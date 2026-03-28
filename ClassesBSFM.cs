using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks; // ADICIONADO: Necessário para o Task.Run
using MimeKit;
using MailKit.Security;
// RESOLUÇÃO DO CONFLITO: Dizemos explicitamente para usar o SmtpClient do MailKit
using SmtpClient = MailKit.Net.Smtp.SmtpClient; 
using System.Net.Http;
using System.Net.Http.Json; // Importante para o JsonContent
using System.Net.Http.Headers;

namespace ClassesBSFM
{
    public class Usuario
    {
        [Key]
        public int ID { get; set; }

        public string Nome { get; set; } = string.Empty;
        public int Idade { get; set; }
        public string Email { get; set; } = string.Empty;

        public string? TokenVerificacao { get; set; }
        public bool EmailVerificado { get; set; } = false;

        public string SenhaHash { get; set; } = string.Empty; 
        public bool AceitouTermos { get; set; }
        public DateTime DataAceite { get; set; }
        public string VersaoTermos { get; set; } = string.Empty;
        public string Sexo { get; set; } = "Não Informado"; 
        public double Peso { get; set; }
        public double Altura { get; set; }
        public string TipoPessoa { get; set; } = "Sedentário";
        public string Intolerancia { get; set; } = string.Empty; 
        
        public double IMC { get; set; }
        public double TMB { get; set; }
        public double GastoTotal { get; set; }

        public Usuario() {
            AceitouTermos = false;
            DataAceite = DateTime.UtcNow; 
        }
    }

    public class CalcularNutricional
    {
        public double CalcularIMC(double peso, double altura)
        {
            if (altura <= 0) return 0;
            return peso / (altura * altura);
        }

        public double CalcularTMB(string sexo, double peso, double alturaMetros, int idade)
        {
            double alturaCm = alturaMetros * 100;
            if (sexo?.ToLower() == "masculino")
                return 88.362 + (13.397 * peso) + (4.799 * alturaCm) - (5.677 * idade);
            else
                return 447.593 + (9.247 * peso) + (3.098 * alturaCm) - (4.330 * idade);
        }

        public double CalcularGastoTotal(string nivelAtividade, double tmb)
        {
            string nivel = nivelAtividade?.ToLower() ?? "";
            if (nivel.Contains("sedentario") || nivel.Contains("sedentário")) return tmb * 1.2;
            if (nivel == "ativo") return tmb * 1.55;
            if (nivel == "muito ativo") return tmb * 1.725;
            return tmb;
        }

        public void RegistrarCalculos(Usuario usuario)
        {
            usuario.IMC = Math.Round(CalcularIMC(usuario.Peso, usuario.Altura), 2);
            usuario.TMB = Math.Round(CalcularTMB(usuario.Sexo, usuario.Peso, usuario.Altura, usuario.Idade), 2);
            usuario.GastoTotal = Math.Round(CalcularGastoTotal(usuario.TipoPessoa, usuario.TMB), 2);
        }
    }

    public class Refeição
    {
        [Key]
        public int ID { get; set; }
        public string NomeRefeição { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty; 
        public string Ingredientes { get; set; } = string.Empty;
        public double Calorias { get; set; }
        public double Proteínas { get; set; }
        public double Carboidratos { get; set; }
        public double Gorduras { get; set; }
    }

    public class Comida
    {
        [Key]
        public int ID { get; set; }
        public string NomeComida { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public double Calorias { get; set; }
        public double Proteínas { get; set; }
        public double Carboidratos { get; set; }
        public double Gorduras { get; set; }
    }

    public class CronogramaAlimentar
    {
        [Key]
        public int ID { get; set; }
        public Usuario? Usuario { get; set; }
        public string Refeições { get; set; } = string.Empty;
        public string Planos { get; set; } = string.Empty;

        public CronogramaAlimentar()
        {
            Refeições = string.Empty;
        }
    }

    public class Hospital
    {
        [Key]
        public int ID { get; set; }
        public string NomeHospital { get; set; } = string.Empty;
        public string Endereço { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
    }

 public class EmailService 
{
    private static readonly HttpClient _httpClient = new HttpClient();

    // DADOS REAIS FORNECIDOS POR VOCÊ
    private const string apiToken = "78c16b8231ead79b43ffaf44838aab1a"; 
    private const string inboxId = "4499518"; 

    public static void EnviarToken(string emailDestino, string token) 
    {
        // _ = Descarta a Task para rodar em background liso
        _ = Task.Run(async () => {
            try {
                // Endpoint oficial de Sandbox do Mailtrap via API
                string url = $"https://sandbox.api.mailtrap.io/api/send/{inboxId}";

                // Payload organizado no padrão que a API deles exige
                var payload = new {
                    to = new[] { 
                        new { email = emailDestino, name = "Usuario BSFM" } 
                    },
                    from = new { 
                        email = "portal@bsfm.io", 
                        name = "BSFM Nutri" 
                    },
                    subject = "🔐 Código de Ativação BSFM",
                    html = $@"
                    <div style='font-family: sans-serif; background-color: #f0fdf4; padding: 40px; text-align: center; border-radius: 20px;'>
                        <h1 style='color: #065f46; font-size: 24px; margin-bottom: 10px;'>Portal Nutricional BSFM</h1>
                        <p style='color: #374151; font-size: 16px;'>Use o código de ativação para liberar seu acesso:</p>
                        <div style='background: #ffffff; padding: 25px; border-radius: 15px; display: inline-block; border: 2px solid #d1fae5; margin: 20px 0;'>
                            <span style='font-size: 36px; font-weight: bold; color: #166534; letter-spacing: 8px;'>{token}</span>
                        </div>
                        <p style='color: #64748b; font-size: 12px;'>Se você não solicitou este e-mail, por favor desconsidere.</p>
                    </div>"
                };

                // Monta a requisição HTTP
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                
                // AUTORIZAÇÃO: É aqui que usamos o seu Token de API
                request.Headers.Add("Api-Token", apiToken);
                
                // SERIALIZA O JSON
                request.Content = JsonContent.Create(payload);

                // Envia pela porta 443 (Internet comum), fugindo do bloqueio SMTP
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode) {
                    Console.WriteLine($"[API MAILTRAP] Código {token} entregue na Sandbox com sucesso!");
                } else {
                    var erroMsg = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API MAILTRAP ERRO] {(int)response.StatusCode} - {erroMsg}");
                }
            }
            catch (Exception ex) {
                // Se o problema persistir, esse log vai dizer o porquê no Railway
                Console.WriteLine($"[API MAILTRAP FATAL]: {ex.Message}");
            }
        });
    }
}
}