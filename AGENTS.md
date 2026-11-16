# AGENTS.md — Tüm Yapay Zekalar İçin Zorunlu Talimat

Bu repo **AbilitySystem** Godot C# addon'udur (Unreal GAS benzeri). Projede birden fazla yapay zeka çalışıyor.
Tag sistemi ayrı bir addon ve ayrı bir repodur: `addons/Tags/` (Godot-AI-Tags). AbilitySystem onu kullanır, tersi yoktur.

**Herhangi bir şey yapmadan önce sırayla oku:**

1. [`Docs/RULES.md`](Docs/RULES.md) — bağlayıcı kurallar
2. [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md) — mimari
3. [`Docs/PROGRESS.md`](Docs/PROGRESS.md) — yapılanlar / yapılmakta olanlar / yapılacaklar

## Hatırlatma (özet — ayrıntı RULES.md'de)
- İsimlerde **`Gameplay` ön eki yok**.
- Modüler, okunaklı; **aşırı soyutlama ve aşırı bağımlılık yok**; tek sınıf her işi yapmaz.
- **Her sınıf `addons/AbilitySystem/<Sinif>/` klasöründe** (ara `Core/`, `Assets/` klasörü yok), sahnesi, varlıkları ve addon ile gelen resource dosyaları da aynı klasörde. Projeye özel resource verisi proje kökündeki `Resource/` klasöründe (resource sınıfı addon içinde).
- **Sahne kuralı:** Önce `Node2D`/`Node`/`Control` ekleyip sonra script bağlamak **yasak**. Düğüm doğrudan özel sınıftan, `script = ExtResource(...)` + `metadata/_custom_type_script = "uid://..."` ile yaratılır. UID'yi `.cs.uid` dosyasından al, uydurma.
- **Performans önemli:** hot-path'te allocation/LINQ/boxing yok.
- Mümkün olduğunca **saf C#** (`List`, `Dictionary`, `event`, `struct`); gereksiz yere Godot `Array`/`Dictionary`/`Variant`/`Signal` kullanma.
- Namespace `AbilitySystem`; tag'ler için `using Tags;`.
- Git komutları bu klasörde çalışır; proje kökü repo değildir. **`Test/` klasörü kullanıcıya aittir, dokunma.**
- `Docs/PROGRESS.md`'yi güncel tut; başkasının "Yapılıyor" işine dokunma.
- Kapsam dışı değişiklik yapma; kuralları kendi başına değiştirme.
