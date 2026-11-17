# Kurallar — AbilitySystem (zorunlu)

## Tasarım
- Unreal GAS benzeri; isimlerde `Gameplay` ön eki yok (`AbilitySystemComponent`, `GameplayAbility` değil).
- Her sistem kendi sınıfında; tek sorumluluk, büyüyen sınıf bölünür.
- Tek kullanımlık interface/factory/manager katmanı yok. Sınıflar birbirini minimum bilir; döngüsel bağımlılık yasak.
- Performans: hot-path'te (her frame/tick) allocation, LINQ, boxing, string işlemi, gereksiz `GetNode` yok. Mümkünse `struct`, önceden ayrılmış koleksiyon, havuz.

## Dil
- Saf C# tercih: `List`, `Dictionary`, dizi, `event`/`Action`, `struct`, `enum`.
- Godot tipleri (`Godot.Collections`, `Variant`, `Callable`, `[Signal]`) yalnızca zorunluysa (export, Resource serileştirme, Godot API); orada nedenini kısa not düş.
- Godot gerektirmeyen mantık düz C# sınıfıdır, `Node` olmaz.

## Klasör
- Her sınıf `addons/AbilitySystem/<Sınıf>/<Sınıf>.cs` (+ `.cs.uid`); ara `Core/`, `Assets/` klasörü yok, tek istisna `Docs/`.
- Sınıfın sahnesi, varlıkları ve addon ile gelen resource'ları aynı klasörde. Sadece o sınıfa ait küçük `enum`/`struct` da orada (ayrı dosyada) olabilir.
- Resource sınıfı addon içinde; projeye özel verisi proje kökündeki `Resource/` altında (repoya girmez). Ör. `AbilityData/AbilityData.cs` ↔ `Resource/Abilities/Fireball.tres`.
- Dosya adı = sınıf adı (`PascalCase`). Yollar `res://addons/AbilitySystem/...`.

## Sahne (kritik)
Önce `Node`/`Node2D`/`Control` ekleyip sonra script bağlamak yasak. Düğüm doğrudan sınıfından yaratılır:
```ini
[ext_resource type="Script" uid="uid://bbf8lyuj8omp1" path="res://addons/AbilitySystem/Projectile/Projectile.cs" id="1_6p3um"]

[node name="Projectile" type="Node2D"]
script = ExtResource("1_6p3um")
metadata/_custom_type_script = "uid://bbf8lyuj8omp1"
```
- UID `.cs.uid` dosyasındakiyle birebir aynı; uydurma. Yoksa Godot'un üretmesini sağla veya kullanıcıdan iste. `.uid` dosyalarını commit et.

## Kod Stili
- Namespace `AbilitySystem`, alt namespace yok. Tag'ler: `using Tags;`.
- `PascalCase` tip/metot/property, `_camelCase` private alan, `camelCase` yerel/parametre.
- `partial` yalnızca Godot sınıflarından türeyen tiplerde. Editör kodu `#if TOOLS` içinde.
- Dosya başına tek ana tip. Erken `return`, derin `if` yok. Yorum yalnızca "neden" için.

## İş Birliği
- `Docs/PROGRESS.md`: başlarken "Yapılıyor"a `[Yapan, tarih]` ile ekle, bitince taşı; başkasının işine dokunma.
- Mimari kararlar ilgili `Docs/Systems/<Sistem>.md` dosyasına yazılır. Kural çelişkisi → düzeltme yerine `PROGRESS.md` notu.
- Kuralları değiştirme (kullanıcıya sor). İstenmeyen refactor, dosya, özellik ekleme.
- Git bu klasörde (`git -C addons/AbilitySystem`). `Test/` kullanıcıya ait.
