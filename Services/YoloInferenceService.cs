using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.Models; // ADICIONADO: Necessário para YoloOptions
using SkiaSharp;

namespace BSFM.Services
{
    public class YoloInferenceService
    {
        private readonly Yolo _yolo;

        public YoloInferenceService()
        {
            var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "yolov10n.onnx");

            // NOVA FORMA DE INICIALIZAR NA VERSÃO 2.0+
            var options = new YoloOptions
            {
                OnnxModel = modelPath,
                ModelType = ModelType.ObjectDetection, // Define que o modelo é para detectar objetos
                Cuda = false, // Força o uso da CPU (Ideal para ARM Oracle Cloud sem GPU)
                GpuId = 0,
            };

            _yolo = new Yolo(options);
        }

        public string DetectarAlimento(byte[] imageBytes) {
            using var ms = new MemoryStream(imageBytes);
            using var image = SKImage.FromEncodedData(ms);
            
            // Roda a detecção normal (IA verá o prato, talheres, etc)
            var results = _yolo.RunObjectDetection(image, 0.25);

            // LISTA DE ITENS QUE SÃO COMIDA NO DATASET (Dataset COCO)
            var itensComidaValidos = new[] { 
                "person", "bicycle", "car", // ... (outros itens que o YOLO detecta mas queremos ignorar)
                "apple", "banana", "orange", "broccoli", "carrot", "hot dog", "pizza", 
                "donut", "cake", "sandwich" 
            };
            
            // LISTA DE UTENSÍLIOS PARA REJEITAR EXPLICITAMENTE
            var utensilios = new[] { "fork", "knife", "spoon", "bowl", "cup", "bottle", "dining table", "chair" };

            // Pegamos o item com maior confiança, DESDE QUE ele não seja um utensílio!
            var detectado = results
                .Where(x => !utensilios.Contains(x.Label.Name.ToLower())) // FILTRO AQUI
                .OrderByDescending(x => x.Confidence)
                .FirstOrDefault();

            // Se ele só detectou garfos e facas, ou nada, retornamos unknown
            return detectado?.Label.Name ?? "unknown";
        }
    }
}