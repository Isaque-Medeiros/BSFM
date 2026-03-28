using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MimeKit;
using MailKit.Security; // IMPORTANTE: Certifique-se de ter este using

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
        public string Categoria { get; set; } 
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
            mensagem.Subject = "Código de Ativação BSFM";

            mensagem.Body = new TextPart("html") {
                Text = $@"
                <div style='font-family: sans-serif; background-color: #f9f9f9; padding: 20px;'>
                    <div style='max-width: 500px; margin: auto; background: white; padding: 30px; border-radius: 10px; border: 1px solid #ddd;'>
                        <h2 style='color: #059669; text-align: center;'>Bem-vindo ao BSFM!</h2>
                        <p style='color: #4b5563; text-align: center;'>Use o código abaixo para ativar sua conta no portal:</p>
                        <div style='background: #ecfdf5; border: 2px dashed #10b981; padding: 15px; font-size: 30px; font-weight: bold; text-align: center; color: #065f46; letter-spacing: 8px;'>
                            {token}
                        </div>
                        <p style='color: #9ca3af; font-size: 12px; text-align: center; margin-top: 30px;'>Se você não solicitou este cadastro, por favor ignore este e-mail.</p>
                    </div>
                </div>"
            };

            using var client = new SmtpClient();
            try {
                // Remova qualquer espaço ou comentário dessa linha:
                client.Connect("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                
                // USE AS 16 LETRAS SEM ESPAÇOS:
                client.Authenticate("isaquemedeiros190406@gmail.com", "nuxogsdedzxknfkd");
                
                client.Send(mensagem);
                client.Disconnect(true);
                Console.WriteLine("[LOG] E-mail enviado com sucesso!");
            }
            catch (Exception ex) {
                // Aqui ele vai detalhar por que o Railway não achou o Gmail
                Console.WriteLine($"[ERRO CRÍTICO E-MAIL]: {ex.Message}");
            }
        }
    }
}