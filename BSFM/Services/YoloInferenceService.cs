using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.Models;
using SkiaSharp;
using System.Linq;
using System.Collections.Generic;

namespace BSFM.Services
{
    public class YoloInferenceService
    {
        private readonly Yolo _yolo;

        // Dicionário Estático: EN -> PT
        // Mudei para public static para que você possa usar Tradutor em outros arquivos se precisar
        public static readonly Dictionary<string, string> Tradutor = new Dictionary<string, string>
        {
            { "banana", "Banana" },
            { "apple", "Maçã" },
            { "sandwich", "Sanduíche" },
            { "orange", "Laranja" },
            { "broccoli", "Brócolis" },
            { "carrot", "Cenoura" },
            { "hot dog", "Cachorro Quente" },
            { "pizza", "Pizza" },
            { "donut", "Rosca Doce" },
            { "cake", "Bolo" }
        };

        private static readonly string[] AlimentosPermitidos = Tradutor.Keys.ToArray();

        public YoloInferenceService()
        {
            var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "yolov10n.onnx");

            var options = new YoloOptions
            {
                OnnxModel = modelPath,
                ModelType = ModelType.ObjectDetection,
                Cuda = false, 
                GpuId = 0,
            };

            _yolo = new Yolo(options);
            Console.WriteLine($"[IA] Inicializada com o modelo: {modelPath}");
        }

        public List<string> DetectarAlimentos(byte[] imageBytes)
        {
            var resultadoFinalPT = new List<string>();

            try 
            {
                if (imageBytes == null || imageBytes.Length == 0) return resultadoFinalPT;

                using var ms = new MemoryStream(imageBytes);
                using var image = SKImage.FromEncodedData(ms);
                
                if (image == null) return resultadoFinalPT;

                // Executa a detecção oficial
                var results = _yolo.RunObjectDetection(image, 0.35);

                // 1. Filtrar o que foi detectado no dataset original (nomes em Inglês)
                var detectadosIngles = results
                    .Where(r => AlimentosPermitidos.Contains(r.Label.Name.ToLower()))
                    .Select(r => r.Label.Name.ToLower())
                    .Distinct()
                    .ToList();

                // 2. Tradução para Português
                foreach (var nomeEn in detectadosIngles)
                {
                    // Busca no dicionário, se não achar (muito difícil) mantém o original
                    string nomePt = Tradutor.ContainsKey(nomeEn) ? Tradutor[nomeEn] : nomeEn;
                    resultadoFinalPT.Add(nomePt);
                }

                if (resultadoFinalPT.Any())
                {
                    Console.WriteLine($"[IA SUCCESS] Traduzidos: {string.Join(", ", resultadoFinalPT)}");
                }

                return resultadoFinalPT;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IA FATAL ERROR] Falha técnica: {ex.Message}");
                return resultadoFinalPT;
            }
        }
    }
}