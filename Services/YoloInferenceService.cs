using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.Models;
using SkiaSharp;
using System.Linq;

namespace BSFM.Services
{
    public class YoloInferenceService
    {
        private readonly Yolo _yolo;

        // LISTA BRANCA: Apenas esses itens do YOLO serão enviados para o cálculo de calorias.
        // O modelo COCO (yolov10) reconhece nativamente esses 10 itens de comida.
        private static readonly string[] AlimentosPermitidos = {
            "banana", "apple", "sandwich", "orange", "broccoli", 
            "carrot", "hot dog", "pizza", "donut", "cake"
        };

        public YoloInferenceService()
        {
            // Caminho dinâmico para garantir que funcione em Windows (Local) e Linux (Railway)
            var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "yolov10n.onnx");

            var options = new YoloOptions
            {
                OnnxModel = modelPath,
                ModelType = ModelType.ObjectDetection,
                Cuda = false, // Garante uso da CPU (mais estável para servidores gratuitos)
                GpuId = 0,
            };

            _yolo = new Yolo(options);
            Console.WriteLine($"[IA] Modelo carregado com sucesso: {modelPath}");
        }

        public string DetectarAlimento(byte[] imageBytes)
        {
            try 
            {
                if (imageBytes == null || imageBytes.Length == 0) return "unknown";

                using var ms = new MemoryStream(imageBytes);
                using var image = SKImage.FromEncodedData(ms);
                
                if (image == null) return "unknown";

                // Executa detecção com 35% de confiança mínima para evitar 'vultos' ou sombras.
                var results = _yolo.RunObjectDetection(image, 0.35);

                // FILTRO INTELIGENTE:
                // 1. Filtra resultados para manter APENAS o que está na nossa lista de comida.
                // 2. Ordena pela maior confiança da IA.
                var alimentoEncontrado = results
                    .Where(r => AlimentosPermitidos.Contains(r.Label.Name.ToLower()))
                    .OrderByDescending(r => r.Confidence)
                    .FirstOrDefault();

                if (alimentoEncontrado != null)
                {
                    Console.WriteLine($"[IA SUCCESS] Detectado: {alimentoEncontrado.Label.Name} ({Math.Round(alimentoEncontrado.Confidence * 100, 1)}%)");
                    return alimentoEncontrado.Label.Name.ToLower();
                }

                Console.WriteLine("[IA INFO] Nenhum alimento permitido detectado na imagem.");
                return "unknown";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IA FATAL ERROR] Erro na inferência: {ex.Message}");
                return "unknown";
            }
        }
    }
}