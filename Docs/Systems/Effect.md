# Effect Sistemi — Plan

> Durum: Faz 0 **uygulandı** (2026-11-17; 40 test, ekle+kaldır+çalıştır döngüsü ≈ 213 ns, 0 allocation). Faz 1 planlandı, Faz 2–3 taslak.
> Karşılığı: Unreal `UGameplayEffect`, `FGameplayEffectSpec`, `FActiveGameplayEffect(sContainer)`, `FGameplayModifierInfo`, `FGameplayEffectModCallbackData`.

## Amaç
- **Instant**: Base'i bir kez değiştirir (hasar, maliyet, init effect).
- **HasDuration / Infinite**: modifier'ları `Aggregator`'a ekler (Current), tag verir; bitince/kaldırılınca geri alır.
- **Periyodik**: süreli/sonsuz effect her periyotta Instant gibi Base'e uygulanır (DoT, regen).

## Kararlar
| Konu | Karar |
|---|---|
| Sınıflar | `EffectData` (Resource) → `EffectSpec` → `ActiveEffect` + `ActiveEffectContainer`; ayrı `Effect` yok. **Kullanıcı** |
| Magnitude | `Modifier` içinde `MagnitudeType` + türe özel alanlar, ilgisiz alanlar gizli. **Kullanıcı** |
| `Aggregator`'ın yeri | `AttributeValue`'dan çıkar, `AttributeSet`'e taşınır (Faz 0). **Kullanıcı** (taşıma), yer: Claude |
| Olay verisi | Hook ve olaylar `EffectSpec` almaz; `ModifierEventData` alır (kaynak, effect verisi, attribute, op, magnitude, eski/yeni değer). Attribute katmanında tanımlı, döngü yok. **Kullanıcı** (spec yok), yapı: Claude |
| Zaman | Ayrı **Timers** addon'u (`TimerManager`); effect'ler süre/periyot için timer kurar, kendi `Tick`'i yok. **Kullanıcı** |
| Veri yapısı | Unreal 5.3 GE Component yerine düz alanlar (sade). |
| Periyot | Periyodik effect'in modifier'ları `Aggregator`'a girmez, her periyotta Base'e uygulanır (Unreal gibi). |
| Magnitude hesabı | Uygulamada bir kez, `ActiveEffect`'te saklanır; spec sonradan değişirse aktif effect etkilenmez. |
| SetByCaller | Anahtar `Tag` (ör. `Data.Damage`). |
| Kaynak | `EffectSpec.Instigator` (sorumlu aktör), `Causer` (ör. mermi); `Node`, isteğe bağlı. |
| Handle | `EffectHandle`, global pozitif sayaç; `Aggregator` `sourceId` = `handle.Value`. Elle modifier ekleyen kod negatif id kullanır. |
| Ağ | Kapsam dışı. |

## Fazlar
| Faz | İçerik |
|---|---|
| 0 | `Aggregator` → `AttributeSet`; `ModifierSource`, `ModifierEventData`, `PostEffectExecute`, modifier olayları, `ExecuteModifier`. |
| — | Timers addon'u (Faz 1'den önce). |
| 1 | Instant / HasDuration / Infinite, periyot, `Constant` + `SetByCaller`, tag'ler, effect olayları. |
| 2 | Stacking, `AttributeBased` magnitude. |
| 3 | Özel magnitude (C#), execution, süregelen tag şartları, bağışıklık. |

## Faz 0 — Attribute Tarafı

### Aggregator → AttributeSet
- `AttributeValue` yalnızca veri olur: `BaseValue`, `CurrentValue`, `BaseValueChanged`, `CurrentValueChanged`. Modifier metotları ve `_aggregator` kalkar.
- `AttributeSet`: `Dictionary<AttributeValue, Aggregator> _aggregators`, ilk modifier'da oluşturulur (modifier almayan set bellek ayırmaz). Anahtar referans; boxing yok, O(1).
- Hesap: `current = aggregator?.Evaluate(base) ?? base`. Tüm modifier'ları kalkan aggregator silinmez (tekrar ekleme maliyeti olmasın).
- Neden set: değişimin tek giriş noktası ve hook'ların sahibi zaten set; container'a koymak set → container geri referansı (döngü) isterdi; set container'sız da çalışır. Unreal'daki gibi effect container'ına koymak attribute hesabını effect katmanına bağlardı.
- Public API'de değişen tek şey `sourceId` yerine `ModifierSource` (aşağıda). Mevcut testler buna göre güncellenir.

### ModifierSource (`readonly struct`, `ModifierSource/`)
Modifier'ı kimin, hangi effect ile uyguladığı:
- `int Id`: `Aggregator` kaynak anahtarı (effect handle'ı; elle eklenen için negatif).
- `Resource Data`: uygulayan effect verisi (`EffectData`). Tip `Resource`: attribute katmanı effect sınıflarını bilmesin (döngü yok); kullanan `data.Source.Data as EffectData` ile alır.
- `Node Instigator`, `Node Causer`: null veya silinmiş olabilir → `GodotObject.IsInstanceValid`.
- `ModifierSource(int id, Resource data = null, Node instigator = null, Node causer = null)`.

### ModifierEventData (`readonly struct`, `ModifierEventData/`)
- `ModifierEventKind Kind`: `Executed` (Base'e uygulandı), `Added`, `Removed` (Current'a eklendi/kaldırıldı). Enum aynı klasörde.
- `AttributeValue Attribute`, `ModifierOp Op`, `float Magnitude`, `float OldValue`, `float NewValue` (Executed'da Base, diğerlerinde Current), `ModifierSource Source`.
- `Removed`'da bir kaynağın o attribute'taki tüm modifier'ları birlikte kalkar: `Op`/`Magnitude` boştur.
- `delegate void ModifierEventHandler(in ModifierEventData data)` aynı klasörde (`in`: kopya yok).

### AttributeSet API
- `SetBaseValue(attribute, value)` (değişmez)
- `AddModifier(attribute, op, magnitude, in ModifierSource source, channel = Channel0)` → `Added`
- `RemoveModifiers(attribute, int sourceId)` → `Removed` (bir şey kalktıysa)
- `ExecuteModifier(attribute, op, magnitude, in ModifierSource source)`: Base = `Aggregator.EvaluateSingle(base, op, m)` → `SetBaseValue` → `PostEffectExecute` → `Executed`
- `protected virtual void PostEffectExecute(in ModifierEventData data)`: yalnızca `Executed`'da, olaydan önce (Unreal sırası: set kuralı önce, dış dinleyici sonra).
- `event ModifierEventHandler ModifierApplied`: üç tür için, değerler yazıldıktan sonra.
- `AttributeContainer`: id'li `AddModifier`/`RemoveModifiers`/`ExecuteModifier` aynı imzayla; `event ModifierEventHandler ModifierApplied` tüm set'lerinkini tek yerden yayınlar (set eklenince abone olur). Oyun kodu (hasar sayısı, öldürme bilgisi) bunu dinler.
- `Aggregator.EvaluateSingle(b, op, m)` (static): `Add`/`AddFinal` → `b + m`, `Multiply`/`MultiplyCompound` → `b × m`, `Divide` → `b ÷ m` (|m| < 1e-6 ise yok), `Override` → `m`.

Meta attribute örneği:
```csharp
protected override void PostEffectExecute(in ModifierEventData data)
{
    if (data.Attribute != Damage)
        return;

    float damage = Damage.BaseValue;
    SetBaseValue(Damage, 0f);
    SetBaseValue(Health, Health.BaseValue - damage);
    // Öldüren: data.Source.Instigator; effect: data.Source.Data as EffectData.
}
```

## Timers Addon'u (ayrı repo, ayrıntılı plan kendi `Docs/`'unda yazılacak)
- `addons/Timers/`, Godot-AI-Timers, namespace `Timers`, bağımsız.
- `TimerManager` (düz C#): `TimerHandle Start(float delay, Action callback, float interval = 0)` (`interval > 0` tekrarlı), `bool Stop(TimerHandle)`, `bool IsActive`, `float GetRemaining`, `Pause`/`Resume`, `void Advance(float delta)`.
- İç yapı: bitiş zamanına göre min-heap; zaman `double` (uzun oturumda float kayması yok). Aynı anda bitenler başlatılma sırasıyla çalışır. Büyük delta'da tekrarlı timer kaçırdığı her tetiklemeyi sırayla yapar. Callback içinde timer kurmak/durdurmak güvenli. `Advance`'te allocation yok.
- `TimerHandle`: `readonly struct` (id + nesil; durmuş handle yanlış timer'ı durdurmaz).
- `TimerRunner` (autoload `Node`): global bir `TimerManager`'ı `_Process`'te ilerletir (oyun duraklayınca durur). Aktöre özel zaman (yavaşlatma) gerekirse kendi manager'ı olabilir.
- Effect kullanımı: süre → tek timer, periyot → tekrarlı timer. Periyot timer'ı süreden önce kurulur, böylece aynı anda düşen son periyot effect kalkmadan çalışır. Callback delegeleri `ActiveEffect` oluşturulurken bir kez bağlanır.

## Faz 1 — Effect Sınıfları

| Sınıf | Tür | Klasör |
|---|---|---|
| `EffectData` + `EffectDurationType` | Resource (`[Tool]`, `[GlobalClass]`) + enum | `EffectData/` |
| `Modifier` + `MagnitudeType` | Resource (`[Tool]`, `[GlobalClass]`) + enum | `Modifier/` |
| `EffectSpec` | düz C# class | `EffectSpec/` |
| `EffectHandle` | readonly struct | `EffectHandle/` |
| `ActiveEffect` | düz C# class | `ActiveEffect/` |
| `ActiveEffectContainer` | düz C# class | `ActiveEffectContainer/` |

### EffectData
- `DurationType` (`Instant`/`HasDuration`/`Infinite`), `Duration` (sn, yalnızca HasDuration).
- Grup `Period`: `Period` (0 = yok), `ExecuteOnApplication = true`. Instant'ta gizli.
- `Modifier[] Modifiers`.
- Grup `Tags` (`TagContainer`): `AssetTags` (effect'in kimliği), `GrantedTags` (aktifken hedefe; Instant'ta gizli), `ApplicationRequiredTags` (hepsi gerekli), `ApplicationBlockedTags` (biri varsa uygulanmaz), `RemoveEffectsWithTags` (uygulanınca Asset/Granted tag'i eşleşen aktif effect'leri kaldırır, hiyerarşik).
- `null` alanlar boş sayılır. `_ValidateProperty` ilgisiz alanları gizler (usage `Storage` kalır, değer kaybolmaz); tür değişince `NotifyPropertyListChanged()`. Çalışma anında değiştirilmez (`.tres` paylaşılır).

### Modifier
- `AttributeName` (menü `AttributeRegistry.BuildOptions()`), `Op`, `Channel` (Instant'ta yok sayılır), `MagnitudeType` (`Constant = 0`, `SetByCaller = 1`), `Value` (Constant), `CallerTagName` (SetByCaller; menü `TagContainer.BuildTagOptions()`).
- Çalışma anı: `AttributeId Attribute`, `Tag CallerTag` ilk erişimde çözülür, önbelleğe alınır. Geçersiz isim → `None`, container yok sayar.

### EffectSpec
- `EffectSpec(EffectData data, Node instigator = null, Node causer = null)`; `Data`, `Instigator`, `Causer`.
- `float Duration { get; set; }`: başlangıç `Data.Duration` (Infinite `+∞`); uygulamadan önce değiştirilebilir (cooldown azaltma).
- `SetCallerMagnitude(Tag, float)`, `TryGetCallerMagnitude(Tag, out float)`: küçük liste, ilk `Set`'te oluşur.
- `float CalculateMagnitude(Modifier)`: Constant → `Value`; SetByCaller → değer, yoksa `GD.PushError` + 0. Public (Ability maliyet kontrolü için).
- Uygulamadan sonra değiştirilmez; aynı spec birden fazla hedefe uygulanabilir.

### ActiveEffect
- Public: `Handle`, `Spec`, `Data`, `Duration`, `RemainingTime` (timer'dan; Infinite `+∞`).
- Internal: `ModifierSource Source` (uygulamada bir kez), `float[] _magnitudes`, süre ve periyot `TimerHandle`'ları, `IsRemoved`.

### ActiveEffectContainer
- `ActiveEffectContainer(AttributeContainer attributes, TagCountContainer tags, TimerManager timers)`
- `EffectHandle Apply(EffectSpec spec)` (başarısız → `None`; Instant'ta geçerli ama listede olmayan handle), `bool RemoveEffect(EffectHandle)`, `int RemoveEffectsWithTags(TagContainer)`, `Count`, indexer, `Get(EffectHandle)`.
- Olaylar: `EffectAdded(ActiveEffect)`, `EffectRemoved(ActiveEffect)`, `EffectExecuted(EffectSpec)`. Attribute düzeyindeki ayrıntı için `AttributeContainer.ModifierApplied`.

**Uygulama:**
1. Spec/Data null → hata, `None`. Tag şartları tutmuyor → `None`. HasDuration ve `Duration ≤ 0` → `None` (hata yok).
2. `RemoveEffectsWithTags` (yeni effect henüz listede değil).
3. Instant: her modifier → `attributes.ExecuteModifier(...)`, sonra `EffectExecuted`.
4. Süreli/sonsuz: `ActiveEffect` oluştur, listeye ekle; periyotsuzsa modifier'lar → `AddModifier`; `GrantedTags` → `tags.Add`; periyot timer'ı, sonra süre timer'ı; `EffectAdded`; periyodik + `ExecuteOnApplication` → bir çalıştırma.

**Kaldırma** (süre timer'ı, `RemoveEffect`, tag ile): `IsRemoved`, timer'ları durdur, periyotsuzsa `RemoveModifiers`, `GrantedTags` → `tags.Remove`, listeden çıkar, `EffectRemoved`.

**Periyot kuralı:** `t = k × Period` anlarında (`k ≥ 1`, `ExecuteOnApplication` ise `k = 0` da), `k × Period ≤ Duration` iken. `Duration 5`, `Period 1` → 6 çalıştırma (kapalıysa 5).

**İç içe:** dinleyiciler effect uygulayıp kaldırabilir. Uygulama sırasında effect kendisi kaldırılırsa kalan adımlar atlanır. Olaylar durum yazıldıktan sonra tetiklenir.

### Kullanım
```csharp
var effects = new ActiveEffectContainer(attributes, tags, TimerRunner.Manager);
var hit = new EffectSpec(fireballDamage, instigator: caster);
hit.SetCallerMagnitude(dataDamage, 35f);  // dataDamage yüklemede bir kez TagRegistry.Request ile
effects.Apply(hit);
EffectHandle haste = effects.Apply(new EffectSpec(hasteData));
effects.RemoveEffect(haste);
```

## Faz 2 (taslak)
- **Stacking**: `None`/`ByTarget`/`BySource` (Instigator), `StackLimit`, süre yenileme, periyot sıfırlama, bitiş politikası. Yığın `n`: `Add`/`AddFinal` `m × n`, `Multiply`/`Divide` `1 + (m − 1) × n`, `MultiplyCompound` `mⁿ`, `Override` `m`. Açık konu: magnitude güncellenirken aggregator sırası (son Override) korunmalı.
- **AttributeBased**: `BackingAttributeName`, `CaptureFrom` (Source/Target), `Coefficient`, `PreMultiplyAdd`, `PostMultiplyAdd`, `Snapshot`. Kaynak her zaman snapshot; hedef canlıysa `CurrentValueChanged` dinlenip modifier güncellenir. Attribute yoksa 0.

## Faz 3 (taslak)
Özel magnitude (kullanıcı resource sınıfı), execution (zırhla azalan hasar), süregelen tag şartları (inhibition), bağışıklık.

## Bu Planın Dışında
ASC (container'ları sahiplenir, başlangıç effect'leri), Cooldown (HasDuration + `Cooldown.X` tag'i, kalan süre sorgusu), Cost ve seviye ölçekleme (Ability planı).

## Bağımlılıklar
```
ActiveEffectContainer ──► ActiveEffect ──► EffectSpec ──► EffectData ──► Modifier ──► AttributeRegistry / AttributeId / ModifierOp
        ├──► AttributeContainer ──► AttributeSet ──► Aggregator, ModifierEventData ──► ModifierSource
        ├──► TagCountContainer, TagContainer, Tag (Tags)
        └──► TimerManager, TimerHandle (Timers)
```
Attribute katmanı effect sınıflarını bilmez (`ModifierSource.Data` tipi `Resource`).

## Performans
- Zaman işi `TimerManager`'da: O(log n) kurma/durdurma, kare başına yalnızca süresi dolan timer'lar.
- Uygulama: süreli effect başına `ActiveEffect` + `float[]` + 2 delege; hazır spec ile Instant 0 allocation hedefi.
- Olaylar `in` struct ile; boxing yok.

## Test
Faz 0: mevcut 57 test (sourceId → `ModifierSource`), olay türleri ve sırası (hook → olay), container olay yayını, 0 allocation.
Faz 1: her op'ta Instant, süreli ekleme/geri alma, periyot 6/5 kuralı ve büyük delta, tag şartları, `RemoveEffectsWithTags`, SetByCaller eksik → 0, iç içe kaldırma. `EffectData`/`Modifier` Godot Resource olduğundan ya Godot API'si stub'lanır ya headless Godot.

## Uygulama Sırası
1. Faz 0: `ModifierSource`, `ModifierEventData` (+ `ModifierEventKind`, `ModifierEventHandler`), `Aggregator.EvaluateSingle`, `AttributeValue`/`AttributeSet`/`AttributeContainer` değişiklikleri, testler, `Attribute.md` güncellemesi.
2. Timers addon'u (repo kurulumu kullanıcıyla; kendi planı).
3. Tags: `BuildTagOptions` public.
4. Faz 1: `EffectHandle` → `Modifier` → `EffectData` → `EffectSpec` → `ActiveEffect` → `ActiveEffectContainer` → testler.
