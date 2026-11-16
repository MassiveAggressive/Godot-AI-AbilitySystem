# Mimari Taslağı

> Durum: **geliştiriliyor**. Tag sistemi uygulandı; diğer sistemler planlama aşamasında. Kararlar değiştikçe bu dosya güncellenir.
> Kurallar için: `Docs/RULES.md`.

## Hedef
Unreal GAS benzeri, Godot C# ile modüler Ability sistemi. Her bileşen ayrı sınıf, ayrı klasör (`Core/<Sinif>/`).

## Planlanan Sistemler (GAS karşılıkları)

| Sınıf | GAS karşılığı | Sorumluluk |
|---|---|---|
| `AbilitySystemComponent` | AbilitySystemComponent | Bir aktörün ability/effect/attribute/tag'lerini bir araya getiren giriş noktası. İşi ilgili alt sistemlere devreder, kendisi mantık taşımaz. |
| `Ability` | GameplayAbility | Ability mantığı: aktivasyon, commit, bitiş/iptal. |
| `AbilityData` (Resource) | — | Ability'nin veri tanımı (cooldown, cost, tag'ler, effect listesi). |
| `AbilitySpec` | GameplayAbilitySpec | Verilmiş bir ability'nin çalışma-zamanı örneği/durumu. |
| `AttributeSet` | AttributeSet | Attribute kümesi (Health, Mana...). |
| `Attribute` | GameplayAttributeData | Base + Current değer. |
| `Effect` | GameplayEffect | Attribute'ları değiştiren etki (Instant / Duration / Infinite). |
| `EffectData` (Resource) | — | Effect'in veri tanımı. |
| `EffectSpec` | GameplayEffectSpec | Uygulanmış effect'in çalışma-zamanı örneği. |
| `Modifier` | Modifier | Attribute üzerinde Add/Multiply/Override işlemi. |
| `Tag` / `TagRegistry` / `TagContainer` / `TagCountContainer` / `TagList` / `TagLoader` | GameplayTag* | Hiyerarşik etiketler ve sorgular (`Ability.Fire.Ball`). Ayrıntı: `Docs/Systems/Tag.md`. |
| `Cooldown` | Cooldown GE | Ability bekleme süresi takibi. |
| `AbilityTask` | AbilityTask | Ability içinde zaman/olay bekleyen asenkron iş parçaları. |
| `AbilityEvent` | GameplayEvent | Ability'leri tetikleyen/olay taşıyan veri (struct). |

Liste başlangıç önerisidir; gereksiz olanlar eklenmez, gerekenler `PROGRESS.md` üzerinden kararlaştırılır.

## Bağımlılık Yönü
```
AbilitySystemComponent
   ├── Ability / AbilitySpec
   ├── Effect / EffectSpec / Modifier
   ├── AttributeSet / Attribute
   └── TagContainer
```
- Alt sistemler `AbilitySystemComponent`'i **bilmez** (gerekirse parametre olarak alır).
- `Tag` ve `Attribute` en alt katman; kimseye bağımlı değildir.

## Veri Akışı (özet)
1. `AbilityData` (.tres, `Resource/` altında) tasarımcı tarafından düzenlenir.
2. Aktöre verilirken `AbilitySpec` oluşturulur.
3. Aktivasyonda tag/cost/cooldown kontrolleri → `Ability` çalışır → `Effect`'ler `EffectSpec` olarak uygulanır → `Modifier`'lar `Attribute`'ları değiştirir.

## Performans Notları
- Tag karşılaştırması string yerine sayısal id ile.
- Aktif effect/ability listeleri önceden ayrılmış `List<T>`; tick'te allocation yok.
- Spec ve event tipleri mümkünse `struct`; gerekirse havuz.
- Godot `Variant`/`Signal` yerine C# `event`/`Action` (gerekmedikçe).

## Karar Günlüğü
| Tarih | Karar | Gerekçe |
|---|---|---|
| 2026-11-16 | `Gameplay` ön eki kullanılmaz | Kullanıcı kuralı |
| 2026-11-16 | Mümkün olduğunca saf C# tipleri | Performans, Godot marshalling maliyeti |
| 2026-11-16 | Tag'ler `Resource/Tags/Tags.tres` içinde, `TagLoader` Autoload ile yüklenir | Kullanıcı kararı; tek kaynak, editörden düzenlenebilir |
| 2026-11-16 | Ağ (replication) kapsam dışı | Kullanıcı kararı |
| 2026-11-16 | `TagContainer` bir `Resource`; başlangıç tag'leri editörde açılır menüden seçilir | Kullanıcı isteği; diğer resource'lar `[Export] TagContainer` kullanır |
| 2026-11-17 | `TagContainer` `List<Tag>` ile kalır; `HashSet` (tag + ebeveyn) ve bitset ölçüldü, seçilmedi | Container'lar küçük, sorgular zaten `TagCountContainer`'da O(1). Ölçüm tablosu: `Docs/Systems/Tag.md` |
| 2026-11-16 | Tag isimleri için `StringName` kullanılmadı | Çalışma anında karşılaştırma zaten `int`; string yalnızca yüklemede. Ayrıntı: `Docs/Systems/Tag.md` |
