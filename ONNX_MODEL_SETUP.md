# ONNX Model Setup Guide

## 🎯 Pretrained ResNet50 Model

Za pravu klasifikaciju slika, trebate koristiti pretrained ResNet50 ONNX model.

## 📥 Download Modela

### Opcija 1: Automatski Download (Preporučeno)
Model će se automatski downloadovati pri prvom korištenju (ako imate internet).

### Opcija 2: Ručni Download
1. Download ResNet50 ONNX model:
   ```
   https://github.com/onnx/models/raw/main/validated/vision/classification/resnet/model/resnet50-v2-7.onnx
   ```

2. Spremite ga u `models/` folder:
   ```
   src/AiAgents.ContentModerationAgent.Web/models/resnet50-v2-7.onnx
   ```

## 🔧 Implementacija

Trenutna implementacija koristi placeholder. Za pravu klasifikaciju, trebate:

1. **Download ONNX model** (automatski ili ručno)
2. **Implementirati ONNX inference** u `DogCatImageClassifier.ClassifyAsync`
3. **Mapirati ImageNet klase** na dog/cat/other

## 📝 ImageNet Klase

- **Dogs**: Klase 151-268 (118 različitih vrsta pasa)
- **Cats**: Klase 281-285 (5 različitih vrsta mačaka)
- **Other**: Sve ostale klase (0-150, 269-280, 286-999)

## 🚀 Quick Start

1. Pokrenite backend - model će se pokušati downloadovati automatski
2. Ako download ne radi, ručno downloadajte model i stavite ga u `models/` folder
3. Implementirajte ONNX inference u `ClassifyAsync` metodi

## ⚠️ Napomena

Trenutna implementacija vraća "other" za sve slike. Za pravu klasifikaciju, trebate implementirati ONNX model inference.
