using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MimeKit;
using MailKit.Security;
// RESOLUÇÃO DO CONFLITO: Dizemos explicitamente para usar o SmtpClient do MailKit
using SmtpClient = MailKit.Net.Smtp.SmtpClient; 

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

        public string SenhaHash { get; set; } = string.Empty; // OK: Combina com Engrenagem e Program
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

   // ADICIONADO 'public' EM TODAS AS CLASSES ABAIXO:
   public class Refeição
   {
        [Key]
        public int ID { get; set; }

        public string NomeRefeição { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty; // Valor inicial
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
            public static void EnviarToken(string emailDestino, string token) 
            {
                var mensagem = new MimeMessage();
                mensagem.From.Add(new MailboxAddress("Portal BSFM", "isaquemedeiros190406@gmail.com"));
                mensagem.To.Add(new MailboxAddress("", emailDestino));
                mensagem.Subject = "🔐 Código de Ativação BSFM";

                mensagem.Body = new TextPart("html") {
                    Text = $"<p>Seu código é: <b>{token}</b></p>"
                };

                // O 'using' agora sabe exatamente que é o SmtpClient do MailKit
                using (var client = new SmtpClient()) 
                {
                    try {
                        client.Timeout = 10000;
                        client.Connect("sandbox.smtp.mailtrap.io", 2525, SecureSocketOptions.StartTls);
                        client.Authenticate("2f43feed9ca5d6", "27f292915287b6");
                        client.Send(mensagem);
                        client.Disconnect(true);
                    }
                    catch (Exception ex) {
                        Console.WriteLine($"[ERRO MAILTRAP]: {ex.Message}");
                    }
                }
            }
        }
    }
}