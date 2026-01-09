# Image Classification - Trenutno Stanje

## ⚠️ Važno

**Trenutna implementacija NE klasificira slike pravilno!**

### Što radi:
- ✅ Slike se uploadaju i spremaju
- ✅ Slike se kompresiraju (max 800x600)
- ✅ Slike se prikazuju u ContentDetails
- ✅ Classification result se sprema u bazu

### Što NE radi:
- ❌ Prava klasifikacija slika (dog/cat/other)
- ❌ Trenutno vraća uvijek "other" i `IsBlocked = false`
- ❌ Slike sa psima NEĆE biti blokirane

## 🔧 Rješenje

Za pravu klasifikaciju, treba implementirati **pretrained model**. Opcije:

### Opcija 1: ONNX Model (Preporučeno)
- Download pretrained ResNet50 ONNX model (ImageNet)
- Koristi `Microsoft.ML.OnnxTransformer`
- Brzo i jednostavno

### Opcija 2: ML.NET Image Classification API
- Koristi transfer learning sa pretrained ResNet50
- Automatski downloaduje model pri prvom korištenju
- Zahtijeva malo više setup-a

### Opcija 3: Brzo treniranje (50-100 slika)
- Mali dataset pasa i mačaka
- Transfer learning sa ResNet50
- Najbolje za custom use case

## 📝 Za Testiranje

Trenutno možete testirati:
1. Upload slike → radi ✅
2. Prikaz slike u ContentDetails → radi ✅
3. Classification result se prikazuje → radi ✅
4. Ali klasifikacija uvijek vraća "other" → ❌

## 🚀 Sljedeći Korak

Implementirati pretrained ONNX model za pravu klasifikaciju.
