using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.Models;
using SkiaSharp;
using System.Linq;
using System.Collections.Generic; // ADICIONADO: Necessário para listas

namespace BSFM.Services
{
    public class YoloInferenceService
    {
        private readonly Yolo _yolo;

        // LISTA BRANCA: O YOLO v10 Oficial (COCO) reconhece esses itens nutricionais.
        private static readonly string[] AlimentosPermitidos = {
            "banana", "apple", "sandwich", "orange", "broccoli", 
            "carrot", "hot dog", "pizza", "donut", "cake"
        };

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
        }

        // ALTERADO: Agora retorna uma Lista de strings (Vários alimentos)
        public List<string> DetectarAlimentos(byte[] imageBytes)
        {
            var listaDetectada = new List<string>();

            try 
            {
                if (imageBytes == null || imageBytes.Length == 0) return listaDetectada;

                using var ms = new MemoryStream(imageBytes);
                using var image = SKImage.FromEncodedData(ms);
                
                if (image == null) return listaDetectada;

                // Executa detecção (Confidence 0.35 para ser preciso)
                var results = _yolo.RunObjectDetection(image, 0.35);

                // FILTRO DE MULTI-DETECÇÃO:
                // Pegamos todos os itens que estão na nossa lista permitida de uma só vez
                listaDetectada = results
                    .Where(r => AlimentosPermitidos.Contains(r.Label.Name.ToLower()))
                    .Select(r => r.Label.Name.ToLower()) // Pega o nome do alimento
                    .Distinct() // Se tiver várias fatias de cenoura, retorna apenas 1 vez o termo "carrot"
                    .ToList();
                                var labelsBrutos = results
                    .Where(r => AlimentosPermitidos.Contains(r.Label.Name.ToLower()))
                    .Select(r => r.Label.Name.ToLower())
                    .Distinct()
                    .ToList();

                // TRADUÇÃO AQUI: Se o nome estiver no tradutor, usa a tradução, senão usa o original
                foreach (var nomeEn in labelsBrutos)
                {
                    string nomePt = Tradutor.ContainsKey(nomeEn) ? Tradutor[nomeEn] : nomeEn;
                    listaDetectada.Add(nomePt);
                }

                return listaDetectada;

                if (listaDetectada.Any())
                {
                    Console.WriteLine($"[IA SUCCESS] Alimentos detectados no prato: {string.Join(", ", listaDetectada)}");
                }

                return listaDetectada;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IA FATAL ERROR] Erro na multi-inferência: {ex.Message}");
                return listaDetectada;
            }
        }

        private static readonly Dictionary<string, string> Tradutor = new Dictionary<string, string>
        {
            { "banana", "Banana" },
            { "apple", "Maçã" },
            { "sandwich", "Sanduíche" },
            { "orange", "Laranja" },
            { "broccoli", "Brócolis" },
            { "carrot", "Cenoura" },
            { "hot dog", "Cachorro Quente" },
            { "pizza", "Pizza" },
            { "donut", "Donut" },
            { "cake", "Bolo" }
        };

        }
    }
}