# Mimari Taslağı

> Durum: **geliştiriliyor**. Tag sistemi uygulandı ve ayrı addon'a (`addons/Tags/`, Godot-AI-Tags) taşındı; diğer sistemler planlama aşamasında. Kararlar değiştikçe bu dosya güncellenir.
> Kurallar için: `Docs/RULES.md`.

## Hedef
Unreal GAS benzeri, Godot C# ile modüler Ability sistemi; `addons/AbilitySystem/` addon'u olarak. Her bileşen ayrı sınıf, ayrı klasör (`addons/AbilitySystem/<Sinif>/`).
Tag'ler bağımsız **Tags** addon'undan gelir (`addons/Tags/`, namespace `Tags`).

## Planlanan Sistemler (GAS karşılıkları)

| Sınıf | GAS karşılığı | Sorumluluk |
|---|---|---|
| `AbilitySystemComponent` | AbilitySystemComponent | Bir aktörün ability/effect/attribute/tag'lerini bir araya getiren giriş noktası. İşi ilgili alt sistemlere devreder, kendisi mantık taşımaz. |
| `Ability` | GameplayAbility | Ability mantığı: aktivasyon, commit, bitiş/iptal. |
| `AbilityData` (Resource) | — | Ability'nin veri tanımı (cooldown, cost, tag'ler, effect listesi). |
| `AbilitySpec` | GameplayAbilitySpec | Verilmiş bir ability'nin çalışma-zamanı örneği/durumu. |
| `AttributeSet` | AttributeSet | Kullanıcının türettiği attribute kümesi (Health, Mana...); clamp/tepki hook'ları. Ayrıntı: `Docs/Systems/Attribute.md`. |
| `AttributeValue` | GameplayAttributeData | Base + Current değer, modifier hesabı. (`Attribute` adı `System.Attribute` ile çakıştığı için.) |
| `AttributeId` / `AttributeRegistry` / `AttributeContainer` | GameplayAttribute / — / — | Attribute kimliği (isim ↔ id), set taraması, aktörün set'leri ve id ile O(1) erişim. |
| `Effect` | GameplayEffect | Attribute'ları değiştiren etki (Instant / Duration / Infinite). |
| `EffectData` (Resource) | — | Effect'in veri tanımı. |
| `EffectSpec` | GameplayEffectSpec | Uygulanmış effect'in çalışma-zamanı örneği. |
| `Modifier` | Modifier | Attribute üzerinde Add/Multiply/Override işlemi. |
| `Tag` / `TagContainer` / `TagCountContainer` ... (**Tags addon'u**) | GameplayTag* | Hiyerarşik etiketler ve sorgular (`Ability.Fire.Ball`). Bu repoda değil; ayrıntı: `addons/Tags/Docs/Tag.md`. |
| `Cooldown` | Cooldown GE | Ability bekleme süresi takibi. |
| `AbilityTask` | AbilityTask | Ability içinde zaman/olay bekleyen asenkron iş parçaları. |
| `AbilityEvent` | GameplayEvent | Ability'leri tetikleyen/olay taşıyan veri (struct). |

Liste başlangıç önerisidir; gereksiz olanlar eklenmez, gerekenler `PROGRESS.md` üzerinden kararlaştırılır.

## Bağımlılık Yönü
```
AbilitySystemComponent
   ├── Ability / AbilitySpec
   ├── Effect / EffectSpec / Modifier
   ├── AttributeContainer / AttributeSet / AttributeValue
   └── TagContainer / TagCountContainer   (Tags addon'u)
```
- Alt sistemler `AbilitySystemComponent`'i **bilmez** (gerekirse parametre olarak alır).
- `Tags` addon'u ve `AttributeValue` en alt katman; kimseye bağımlı değildir (`AttributeValue` yalnızca `ModifierOp` enum'unu kullanır). `Tags`, AbilitySystem'i bilmez.

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
| 2026-11-17 | Proje iki addon'a bölündü: `addons/AbilitySystem` (bu repo) ve `addons/Tags` (Godot-AI-Tags); her biri ayrı git reposu, proje kökü repoya girmez | Kullanıcı isteği; repolarda sadece ilgili scriptler olsun |
| 2026-11-17 | Tags namespace'i `Tags`; tag listesi projede (`res://Resource/Tags/Tags.tres`), yolu `tags/tag_list_path` project setting'inden okunur | Kullanıcı kararı; addon başka projelerde temiz kullanılsın |
| 2026-11-17 | Addon içinde `Core/` ve `Assets/` yok; sınıf klasörleri doğrudan addon kökünde (`Tags/Tag/`, `AbilitySystem/AttributeValue/`) | Kullanıcı kararı; addon'lar sade kalsın |
| 2026-11-17 | Attribute'lar `AttributeSet`'ten türeyen C# sınıflarında property olarak tanımlanır (Unreal gibi); veride `SetAdı.Attribute` açılır menüden seçilir | Kullanıcı kararı; tipli erişim, kurallar override ile. Ayrıntı: `Docs/Systems/Attribute.md` |
| 2026-11-17 | Çarpan modifier'ları için iki ayrı işlem: `MultiplyAdditive` (toplanır) ve `MultiplyCompound` (çarpılır) | Kullanıcı kararı |
| 2026-11-17 | Attribute değer sınıfının adı `AttributeValue` | `Attribute` adı, kullanıcı kodunda `using System;` varken CS0104 (belirsiz başvuru) veriyor; derlenerek denendi |
| 2026-11-17 | Aktöre özel başlangıç değerleri init effect ile (ASC'de başlangıç effect listesi) | Unreal ile aynı; attribute tarafına ek sınıf gerekmez. Resource tabanlı set fikri ertelendi: `Docs/Systems/Attribute.md` |
| 2026-11-16 | `Gameplay` ön eki kullanılmaz | Kullanıcı kuralı |
| 2026-11-16 | Mümkün olduğunca saf C# tipleri | Performans, Godot marshalling maliyeti |
| 2026-11-16 | Tag'ler `Resource/Tags/Tags.tres` içinde, `TagLoader` Autoload ile yüklenir | Kullanıcı kararı; tek kaynak, editörden düzenlenebilir |
| 2026-11-16 | Ağ (replication) kapsam dışı | Kullanıcı kararı |
| 2026-11-16 | `TagContainer` bir `Resource`; başlangıç tag'leri editörde açılır menüden seçilir | Kullanıcı isteği; diğer resource'lar `[Export] TagContainer` kullanır |
| 2026-11-17 | `TagContainer` `List<Tag>` ile kalır; `HashSet` (tag + ebeveyn) ve bitset ölçüldü, seçilmedi | Container'lar küçük, sorgular zaten `TagCountContainer`'da O(1). Ölçüm tablosu: `addons/Tags/Docs/Tag.md` |
| 2026-11-16 | Tag isimleri için `StringName` kullanılmadı | Çalışma anında karşılaştırma zaten `int`; string yalnızca yüklemede. Ayrıntı: `addons/Tags/Docs/Tag.md` |
