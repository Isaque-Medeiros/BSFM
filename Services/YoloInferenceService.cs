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
            var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "yolov11n.onnx");

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

        public string DetectarAlimento(byte[] imageBytes)
        {
            using var ms = new MemoryStream(imageBytes);
            using var image = SKImage.FromEncodedData(ms);
            
            // Inferência com 0.25 de threshold de confiança
            var results = _yolo.RunObjectDetection(image, 0.25);

            // Pega o rótulo com maior confiança
            var detectado = results.OrderByDescending(x => x.Confidence).FirstOrDefault();

            return detectado?.Label.Name ?? "unknown";
        }
    }
}