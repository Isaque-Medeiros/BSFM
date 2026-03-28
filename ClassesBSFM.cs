using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks; // ADICIONADO: Necessário para o Task.Run
using MimeKit;
using MailKit.Security;
// RESOLUÇÃO DO CONFLITO: Dizemos explicitamente para usar o SmtpClient do MailKit
using SmtpClient = MailKit.Net.Smtp.SmtpClient; 
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

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
    // Crie um HttpClient estático para ser ultra-rápido
    private static readonly HttpClient _httpClient = new HttpClient();

    public static void EnviarToken(string emailDestino, string token) 
    {
        _ = Task.Run(async () => {
            try {
                // Aqui montamos o corpo do e-mail no formato que a API do Mailtrap espera
                var payload = new {
                    to = new[] { new { email = emailDestino, name = "Usuario" } },
                    from = new { email = "contato@bsfmnutri.io", name = "Portal BSFM" },
                    subject = "🔐 Código de Ativação BSFM",
                    html = $@"<div style='font-family:sans-serif;background:#f0fdf4;padding:40px;border-radius:20px;text-align:center;'>
                                <h2 style='color:#065f46'>Seu código BSFM</h2>
                                <h1 style='color:#166534;font-size:32px;letter-spacing:5px;'>{token}</h1>
                             </div>"
                };

                // No Mailtrap Sandbox, enviamos para este endpoint especial
                var url = "https://send.api.mailtrap.io/api/send";

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                
                // --- MUITO IMPORTANTE ---
                // Para API, você usa um "Api-Token" que está na sua tela de integração do Mailtrap
                // Ele costuma estar na aba "API" (e não na aba SMTP). 
                // Se o seu username era "2f43feed9ca5d6", seu token de API é o mesmo para teste.
                request.Headers.Add("Api-Token", "2f43feed9ca5d627f292915287b6933"); 
                // (O token costuma ser a junção das credenciais ou gerado na aba API)

                request.Content = JsonContent.Create(payload);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode) {
                    Console.WriteLine($"[API SUCESSO] E-mail enviado via HTTP para {emailDestino}");
                } else {
                    var erro = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API ERRO]: {erro}");
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"[API EXCEPTION]: {ex.Message}");
            }
        });
    }
}
}