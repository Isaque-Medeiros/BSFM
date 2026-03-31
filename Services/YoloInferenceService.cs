using YoloDotNet;
using YoloDotNet.Enums;
using SkiaSharp; // O YoloDotNet usa Skia para processamento de imagem

namespace BSFM.Services
{
    public class YoloInferenceService
    {
        private readonly Yolo _yolo;
        private readonly string _modelPath;

        public YoloInferenceService()
        {
            // Caminho relativo ao executável para rodar em Docker/Linux
            _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "yolo11n.onnx");
            
            // Instancia o Yolo para rodar em CPU (Zero Custo GPU)
            _yolo = new Yolo(_modelPath); 
        }

        public string DetectarAlimento(byte[] imageBytes)
        {
            using var ms = new MemoryStream(imageBytes);
            using var image = SKImage.FromEncodedData(ms);
            
            // Realiza a inferência (Yolov11 usa tipagem ObjectDetection)
            var results = _yolo.RunObjectDetection(image, 0.25d); // 0.25 é o threshold de confiança

            // Retorna o item com maior confiança que esteja na categoria "comida" simplificada do COCO
            // Exemplos de labels COCO: "apple", "banana", "sandwich", "orange", "broccoli", "carrot", "pizza", "donut", "cake"
            var detectado = results.OrderByDescending(x => x.Confidence).FirstOrDefault();

            return detectado?.Label.Name ?? "unknown";
        }
    }


}