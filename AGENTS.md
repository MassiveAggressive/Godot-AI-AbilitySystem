# AGENTS.md — Tüm Yapay Zekalar İçin Zorunlu Talimat

Bu projede (Godot C# AbilitySystem, Unreal GAS benzeri) birden fazla yapay zeka çalışıyor.
**Herhangi bir şey yapmadan önce sırayla oku:**

1. [`Docs/RULES.md`](Docs/RULES.md) — bağlayıcı kurallar
2. [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md) — mimari
3. [`Docs/PROGRESS.md`](Docs/PROGRESS.md) — yapılanlar / yapılmakta olanlar / yapılacaklar

## Hatırlatma (özet — ayrıntı RULES.md'de)
- İsimlerde **`Gameplay` ön eki yok**.
- Modüler, okunaklı; **aşırı soyutlama ve aşırı bağımlılık yok**; tek sınıf her işi yapmaz.
- **Her sınıf `Core/<Sinif>/` klasöründe**, sahnesi de aynı klasörde. Assetler `Assets/`, resource dosyaları `Resource/` (resource sınıfı yine `Core/`'da).
- **Sahne kuralı:** Önce `Node2D`/`Node`/`Control` ekleyip sonra script bağlamak **yasak**. Düğüm doğrudan özel sınıftan, `script = ExtResource(...)` + `metadata/_custom_type_script = "uid://..."` ile yaratılır. UID'yi `.cs.uid` dosyasından al, uydurma.
- **Performans önemli:** hot-path'te allocation/LINQ/boxing yok.
- Mümkün olduğunca **saf C#** (`List`, `Dictionary`, `event`, `struct`); gereksiz yere Godot `Array`/`Dictionary`/`Variant`/`Signal` kullanma.
- `Docs/PROGRESS.md`'yi güncel tut; başkasının "Yapılıyor" işine dokunma.
- Kapsam dışı değişiklik yapma; kuralları kendi başına değiştirme.
