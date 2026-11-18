# Effect Sistemi — Plan

> Durum: Faz 0–3 **uygulandı** (2026-11-17). Faz 1: Godot içinde (headless) 48 test; hazır spec ile Instant uygulama (2 modifier, biri AttributeBased) ≈ 326 ns, Instant uygulama ve periyodik tick 0 allocation.
>
> Uygulamada netleşenler (Faz 1):
> - `EffectSpec.SetCallerMagnitude` spec'i döner (zincirleme): `effects.Apply(new EffectSpec(data, caster).SetCallerMagnitude(tag, 35f))`. Constructor: `(data, instigator, causer, instigatorAbilitySystem)`.
> - `ActiveEffect.IsRemoved`: elde tutulan referansın effect'i hâlâ aktif mi.
> - `EffectData.IsInstant`, `IsPeriodic` yardımcıları.
> - Tag'ler tek tek verilir ve sayılır: uygulama sırasında bir dinleyici effect'i kaldırırsa yalnızca verilmiş tag'ler geri alınır (başka kaynağın tag'i düşmez).
> - `RemoveEffectsWithTags` her kaldırmadan sonra listeyi baştan tarar (dinleyiciler listeyi değiştirebilir) ve çağrı sırasında uygulanan effect'lere dokunmaz (yeniden uygulayan dinleyici döngüye girmez).
> - `Modifiers` dizisindeki boş elemanlar atlanır.
> Karşılığı: Unreal `UGameplayEffect`, `FGameplayEffectSpec`, `FActiveGameplayEffect(sContainer)`, `FGameplayModifierInfo`, `FGameplayEffectModCallbackData`.

## Amaç
- **Instant**: Base'i bir kez değiştirir (hasar, maliyet, init effect).
- **HasDuration / Infinite**: modifier'ları `Aggregator`'a ekler (Current), tag verir; bitince/kaldırılınca geri alır.
- **Periyodik**: süreli/sonsuz effect her periyotta Instant gibi Base'e uygulanır (DoT, regen).

## Kararlar
| Konu | Karar |
|---|---|
| Sınıflar | `EffectData` (Resource) → `EffectSpec` → `ActiveEffect` + `ActiveEffectContainer`; ayrı `Effect` yok. **Kullanıcı** |
| Magnitude | Ayrı `Magnitude` resource'u; tür (Constant/SetByCaller/AttributeBased) içinde seçilir, ilgisiz alanlar gizli. Modifier, süre ve periyot bunu kullanır. **Kullanıcı** |
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
| 2 | Stacking. |
| 3 | Özel magnitude (C#), execution, süregelen tag şartları, kaldırma şartı, bağışıklık. |

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
- `Node Instigator`, `Node Causer`, `Node InstigatorAbilitySystem`: null veya silinmiş olabilir → `GodotObject.IsInstanceValid`. Üçüncüsü uygulayanın ASC'si; attribute katmanı ASC'yi bilmesin diye `Node` tipinde, kullanan `as AbilitySystemComponent` ile alır. `EffectSpec`'e de aynı alan eklenir (ASC planında netleşir).
- `ModifierSource(int id, Resource data = null, Node instigator = null, Node causer = null, Node instigatorAbilitySystem = null)`.

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

## Zaman: Timers Addon'u (ayrıntı `addons/Timers/Docs/Timers.md`)
- Statik `TimerManager.Start/Stop/GetRemaining` (autoload ilerletir, oyun duraklayınca durur).
- Süre timer'ının callback'inde periyot timer'ının kalan süresi ≤ 1e-4 ise son periyot effect kalkmadan çalıştırılır (float girdilerde tam eşitlik garanti değil).
- Effect kullanımı: süre → tek timer, periyot → tekrarlı timer. Periyot timer'ı süreden önce kurulur, böylece aynı anda düşen son periyot effect kalkmadan çalışır. Callback delegeleri `ActiveEffect` oluşturulurken bir kez bağlanır.

## Faz 1 — Effect Sınıfları

| Sınıf | Tür | Klasör |
|---|---|---|
| `EffectData` + `EffectDurationType` | Resource (`[Tool]`, `[GlobalClass]`) + enum | `EffectData/` |
| `Modifier` | Resource (`[Tool]`, `[GlobalClass]`) | `Modifier/` |
| `Magnitude` + `MagnitudeType` + `AttributeCaptureSource` | Resource (`[Tool]`, `[GlobalClass]`) + enum'lar | `Magnitude/` |
| `EffectSpec` | düz C# class | `EffectSpec/` |
| `EffectHandle` | readonly struct | `EffectHandle/` |
| `ActiveEffect` | düz C# class | `ActiveEffect/` |
| `ActiveEffectContainer` | düz C# class | `ActiveEffectContainer/` |

### EffectData (inspector grupları: Duration, Modifiers, Tags) — kullanıcı kararı
- Grup `Duration`: `DurationType` (`Instant`/`HasDuration`/`Infinite`), `Duration` (`Magnitude`, sn; yalnızca HasDuration'da görünür, orada zorunlu), `Period` (`Magnitude`, sn; boş = periyot yok; Instant'ta gizli), `ExecuteOnApplication = true` (yalnızca periyot varken görünür).
- Süre ve periyot uygulama anında hedefe karşı hesaplanır, effect boyunca sabittir. Süre ≤ 0 → uygulanmaz; periyot ≤ 0 → periyotsuz sayılır.
- Grup `Modifiers`: `Modifier[] Modifiers`.
- Grup `Tags` (`TagContainer`): `AssetTags` (effect'in kimliği), `GrantedTags` (aktifken hedefe; Instant'ta gizli), `ApplicationRequiredTags` (hepsi gerekli), `ApplicationBlockedTags` (biri varsa uygulanmaz), `RemoveEffectsWithTags` (uygulanınca Asset/Granted tag'i eşleşen aktif effect'leri kaldırır, hiyerarşik).
- `null` alanlar boş sayılır. `_ValidateProperty` ilgisiz alanları gizler (usage `Storage` kalır, değer kaybolmaz); tür değişince `NotifyPropertyListChanged()`. Çalışma anında değiştirilmez (`.tres` paylaşılır).

### Modifier
- `AttributeName` (menü `AttributeRegistry.BuildOptions()`), `Op`, `Magnitude` (resource), `Channel` (Instant/periyotta yok sayılır).
- Çalışma anı: `AttributeId Attribute` ilk erişimde çözülür, önbelleğe alınır. Geçersiz isim → `None`, container yok sayar. `Magnitude` boşsa 0 + hata.

### Magnitude (Resource) — kullanıcı kararı
Modifier büyüklüğü, effect süresi ve periyodu için ortak sayı tanımı. Tür resource içinde seçilir, yalnızca o türün alanları görünür:
- `Constant`: `Value`.
- `SetByCaller`: `CallerTagName` (tag menüsü); değer `spec.SetCallerMagnitude(tag, v)` ile verilir, yoksa 0 + hata.
- `AttributeBased`: `BackingAttributeName` (attribute menüsü), `CaptureFrom` (`Source` = uygulayan, `Target` = alan), `Coefficient = 1`, `Snapshot = true`. Sonuç `Coefficient × CurrentValue`. Kaynaktan okumak için `spec.SourceAttributes` gerekir (yoksa 0 + hata); aktörde attribute yoksa 0, hata yok.
- `Snapshot` (kullanıcı kararı): açık → uygulamada bir kez okunur. Kapalı → her kullanımda: Instant'ta o modifier çalışmadan hemen önce, periyotta her çalıştırmada, süreli/sonsuzda attribute değiştikçe (`CurrentValueChanged` dinlenir, `Aggregator`'da sırası korunarak `UpdateModifier`, olay `Updated`). Kendi attribute'una bağlı modifier takip edilmez (uyarı; sonsuz büyürdü). Süre ve periyot her zaman bir kez okunur.
- Çalışma anı: `Tag CallerTag`, `AttributeId BackingAttribute` ilk erişimde çözülür.

### EffectSpec
- `EffectSpec(EffectData data, Node instigator = null, Node causer = null, Node instigatorAbilitySystem = null)`; `AttributeContainer SourceAttributes { get; set; }`.
- `SetCallerMagnitude(Tag, float)` (spec'i döner), `TryGetCallerMagnitude(Tag, out float)`: küçük liste, ilk `Set`'te oluşur.
- `float CalculateMagnitude(Magnitude, AttributeContainer target)`: public (Ability maliyet kontrolü için).
- Instant'ta tüm magnitude'lar modifier'lar çalışmadan önce hesaplanır (hedeften okunan değer aynı effect'in önceki modifier'ından etkilenmez); 16 modifier'a kadar stack'te.
- Uygulamadan sonra değiştirilmez; aynı spec birden fazla hedefe uygulanabilir, her hedef magnitude'ları kendine karşı hesaplar.

### ActiveEffect
- Public: `Handle`, `Spec`, `Data`, `Duration`, `RemainingTime` (timer'dan; Infinite `+∞`).
- Internal: `ModifierSource Source` (uygulamada bir kez), `float[] _magnitudes`, süre ve periyot `TimerHandle`'ları, `IsRemoved`.

### ActiveEffectContainer
- `ActiveEffectContainer(AttributeContainer attributes, TagCountContainer tags)`; zaman için statik `TimerManager`.
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
var effects = new ActiveEffectContainer(attributes, tags);
var hit = new EffectSpec(fireballDamage, instigator: caster);
hit.SetCallerMagnitude(dataDamage, 35f);  // dataDamage yüklemede bir kez TagRegistry.Request ile
effects.Apply(hit);
EffectHandle haste = effects.Apply(new EffectSpec(hasteData));
effects.RemoveEffect(haste);
```

## Faz 2 — Stacking (uygulandı, 18 test)
- `EffectData` grubu `Stacking` (Instant'ta gizli): `StackingType` (`None`/`ByTarget`/`BySource` = her `Instigator` için ayrı), `StackLimit` (0 = sınırsız), `RefreshDurationOnStack = true`, `ResetPeriodOnStack = true` (Unreal varsayılanı; **kullanıcı kararı**), `StackExpiration` (`ClearStack` / `RemoveOneAndRefresh` / `RefreshDuration`).
- Yığına eklenen uygulama mevcut handle'ı döner; yığın kendi magnitude ve süresini korur (Unreal gibi), yeni spec yalnızca sayıyı artırır. Sınırdayken yalnızca yeniler. Yığın eklenince periyodik effect ayrıca çalışmaz.
- Yığın `n`: `Aggregator.StackMagnitude` → `Add`/`AddFinal` `m × n`, `Multiply`/`Divide` `1 + (m − 1) × n`, `MultiplyCompound` `mⁿ`, `Override` `m`. Modifier'lar sırası korunarak güncellenir (`UpdateModifier`); periyodik effect her çalıştırmada yığınlı değeri kullanır.
- `RemoveEffect(handle, stacks = hepsi)`, `ActiveEffect.StackCount`, olay `StackCountChanged(effect, eski, yeni)`. Yığın ekleme 0 allocation.

## Faz 3 — Hesaplama, Süregelen Şartlar, Bağışıklık (uygulandı, 60 test)
Karşılığı: Unreal `UGameplayModMagnitudeCalculation`, `UGameplayEffectExecutionCalculation`, `OngoingTagRequirements` (inhibition), `UImmunityGameplayEffectComponent`. Attribute katmanı değişmez.

> Uygulamada netleşenler:
> - `CalculationContext` constructor'ı public (`targetTags`, `stackCount` isteğe bağlı): kullanıcı kendi hesabını container'sız deneyebilir.
> - Etkisiz başlayan effect `InhibitionChanged` tetiklemez; `EffectAdded`'da `IsInhibited`'a bakılır.
> - Şartlı effect, kurulumu (modifier, tag) bitince izlenmeye başlar ve hemen bir kez kontrol edilir; yarım uygulanmış effect etkisizleşmez. Kontrol listenin kopyası üzerinde yürür.
> - Tag dinlemesi son şartlı effect kalkınca bırakılır.
> - `Magnitude.Calculation` ve `EffectData.Executions` `Resource` tipinde export edilir (tür kısıtı hint ile): editörde `[Tool]` olmayan alt sınıf yalnızca yer tutucu `Resource` olur ve hesaplama tipindeki alana atanamaz (`InvalidCastException`). Tip dönüşümü çalışma anında; kullanıcı alt sınıfına `[Tool]` yazmak zorunda değil.
> - Periyot ortasında effect kaldırılsa da o periyodun kalan modifier ve execution çıktıları uygulanır (Faz 1 davranışı).
> - Ölçüm: Instant execution ve periyodik execution 0 allocation. Godot 4.7 soyut sınıfları `is_abstract: true` kaydeder (inspector menüsü editörde denenecek).

| Sınıf | Tür | Klasör |
|---|---|---|
| `CalculationContext` | readonly struct | `CalculationContext/` |
| `MagnitudeCalculation` | abstract Resource (`[GlobalClass]`) | `MagnitudeCalculation/` |
| `ExecutionCalculation` | abstract Resource (`[GlobalClass]`) | `ExecutionCalculation/` |
| `ExecutionOutput` | sealed class | `ExecutionOutput/` |

### CalculationContext
Hesaplamaya verilen bilgi, `in` ile (kopya yok). Struct, böylece alan eklemek kullanıcı override'larını bozmaz.
- `EffectSpec Spec` (Data, Instigator, Causer, SetByCaller), `SourceAttributes`, `SourceTags` (spec'ten), `TargetAttributes`, `TargetTags`, `int StackCount` (Instant'ta 1). Kaynak alanları ve `TargetTags` null olabilir.
- `float GetSourceValue(AttributeId)`, `GetTargetValue(AttributeId)`: current; container veya attribute yoksa 0.
- `EffectSpec` ekleri: `TagCountContainer SourceTags { get; set; }` (`SourceAttributes` gibi, ASC dolduracak); `CalculateMagnitude(magnitude, target, TagCountContainer targetTags = null)`.

### Özel magnitude
- `MagnitudeType.Custom`, `Magnitude.Calculation` (`MagnitudeCalculation`, yalnızca Custom'da görünür; boşsa 0 + hata).
- `public abstract float Calculate(in CalculationContext context)`: yığın başına değer (yığın çarpımı yine `Aggregator.StackMagnitude`'da).
- Kullanıcı `[GlobalClass]` alt sınıf yazar, ayarlarını export alanı olarak ekler; inspector'ın "Yeni …" menüsünde çıkar. Resource paylaşıldığı için durum tutmaz.
- `Snapshot` Custom'da da görünür (**kullanıcı kararı**): açık → uygulamada bir kez; kapalı → Instant'ta o modifier'dan hemen önce, periyotta her çalıştırmada. Süreli/sonsuzda bir kez hesaplanır, takip yok (hesabın hangi attribute'lara baktığı bilinmez). Süre ve periyot her zaman bir kez.

### Execution
- `EffectData.Executions` (`ExecutionCalculation[]`, grup `Modifiers`): yalnızca Instant ve periyodik effect'te; diğerlerinde gizli ve yok sayılır (Unreal gibi). Boş elemanlar atlanır.
- `public abstract void Execute(in CalculationContext context, ExecutionOutput output)`; `ExecutionOutput.AddModifier(AttributeId, ModifierOp, float)`: Base'e uygulanacak değişiklik.
- Sıra (Unreal gibi): effect'in modifier'ları → her execution için `Execute`, dönünce çıktıları sırayla `ExecuteModifier` (aynı `ModifierSource`, yani `PostEffectExecute` ve `ModifierApplied` effect'i görür) → `EffectExecuted`. Execution modifier'ların yazdığı değerleri görür; kendi çıktıları hesap bitince uygulanır.
- Yığın: çıktı otomatik çarpılmaz, execution `context.StackCount`'a bakar.
- Bellek: `ExecutionOutput` iç içe çağrı derinliği başına havuzdan (dinleyici çıktı uygulanırken başka execution çalıştırabilir); ısındıktan sonra 0 allocation.

```csharp
// Zırhla azalan hasar: Damage meta attribute'una yazar, PostEffectExecute Health'e geçirir.
[GlobalClass]
public partial class ArmorDamage : ExecutionCalculation
{
    private static readonly AttributeId AttackPower = AttributeRegistry.Request("CombatSet.AttackPower");
    private static readonly AttributeId Armor = AttributeRegistry.Request("CombatSet.Armor");
    private static readonly AttributeId Damage = AttributeRegistry.Request("CombatSet.Damage");

    [Export] public float ArmorConstant { get; set; } = 100f;

    public override void Execute(in CalculationContext context, ExecutionOutput output)
    {
        float armor = context.GetTargetValue(Armor);
        output.AddModifier(Damage, ModifierOp.Add, context.GetSourceValue(AttackPower) * ArmorConstant / (ArmorConstant + armor));
    }
}
```

### Süregelen tag şartları (inhibition) ve kaldırma şartı
- Grup `Tags`: `OngoingRequiredTags` (hepsi), `OngoingBlockedTags` (biri), `RemovalTags` (biri); Instant'ta gizli.
- `RemovalTags` (**kullanıcı kararı**, Unreal `RemovalTagRequirements`): hedefe bunlardan biri gelince effect tüm yığınıyla kaldırılır (etkisizken de). Hedefte zaten varsa uygulanmaz (`None`).
- Uygulama şartları uygulanıp uygulanmayacağını belirler; süregelen şart tutmuyorsa effect yine eklenir, **etkisiz** başlar (Unreal gibi).
- Etkisizken: modifier'lar `Aggregator`'dan çıkar, granted tag'ler geri alınır, periyot çalıştırmaları atlanır (timer işler; etkinleşince periyot sıfırlanmaz, Unreal varsayılanı `NeverReset`), bağışıklık vermez. Süre işler; yığın eklenip düşebilir, takip edilen attribute'lar magnitude'u güncellemeye devam eder (yalnızca saklanır).
- Etkinleşince modifier'lar güncel yığınlı değerle yeniden eklenir (`Aggregator`'da sona geçer), tag'ler geri verilir.
- `ActiveEffect.IsInhibited`, olay `InhibitionChanged(ActiveEffect, bool inhibited)`.
- Dinleme: ilk şartlı effect (süregelen veya kaldırma şartı olan) eklenince `TagCountContainer.PresenceChanged`'e abone olunur; yalnızca şartlı effect'ler (ayrı liste) yeniden kontrol edilir, şartsızlara maliyet yok.
- İç içe: etkisizleştirme tag kaldırır, bu da yeni `PresenceChanged` doğurur. Kontrol sürerken gelen değişiklik yalnızca "tekrar kontrol" işaretler; en fazla 8 tur, sonra uyarı (kendi tag'iyle kendini engelleyen effect sonsuz döngüye girmez).

### Bağışıklık
- Grup `Tags`: `ImmunityTags` (Instant'ta gizli). Effect aktif ve etkinken `AssetTags`'ı bunlardan biriyle eşleşen (hiyerarşik: `Effect.Damage`, `Effect.Damage.Fire`'ı da tutar) yeni effect'ler uygulanmaz. Aynı `EffectData` muaf (kendi yığını/yenilemesi engellenmez).
- Engellenince `None` ve olay `ImmunityBlocked(EffectSpec blocked, ActiveEffect immunity)`.
- Aktif effect'leri kaldırmaz; temizlemek için aynı effect'e `RemoveEffectsWithTags`.
- Maliyet: bağışıklık veren aktif effect yoksa kontrol atlanır.

### Akış (Faz 1 adımlarının yerine)
1. Spec/Data null → hata. Uygulama tag şartları veya `RemovalTags` hedefte → `None`. Bağışıklık → `ImmunityBlocked`, `None`. HasDuration ve süre ≤ 0 → `None`.
2. `RemoveEffectsWithTags`, sonra yığın (Faz 2).
3. Instant: modifier'lar → execution'lar → `EffectExecuted`.
4. Süreli/sonsuz: `ActiveEffect`; süregelen şart tutmuyorsa modifier ve tag atlanır (etkisiz); timer'lar; `EffectAdded`; periyodik + `ExecuteOnApplication` + etkin → bir çalıştırma.
5. Periyot: etkisizse atlanır; değilse modifier'lar → execution'lar → `EffectExecuted`.

### Test
- Custom: değer, boş `Calculation` → 0 + hata, snapshot davranışı, süre/periyot olarak kullanım.
- Execution: Instant ve periyotta çıktı, sıra (modifier → execution), `PostEffectExecute`'ta kaynak, yığın sayısı, iç içe execution, ısındıktan sonra 0 allocation.
- Inhibition: etkisiz başlama, tag gelince/gidince modifier ve tag, periyot atlama, süre işler, etkisizken yığın, kendini engelleyen effect.
- Kaldırma şartı: tag gelince kaldırma (etkisizken de), tag zaten varken uygulanmama.
- Bağışıklık: engelleme ve olay, hiyerarşik eşleşme, etkisizken vermez, kendi yığını, aktif effect'e dokunmaz.
- Editörde: abstract `[GlobalClass]` sınıfların "Yeni …" menüsü (Godot 4.7), yeni alanların gizlenmesi.

### Uygulama sırası
1. `CalculationContext`, `EffectSpec` ekleri. 2. `MagnitudeCalculation`, `MagnitudeType.Custom`. 3. `ExecutionCalculation`, `ExecutionOutput`, `EffectData.Executions`. 4. Inhibition, `RemovalTags`. 5. Bağışıklık. 6. Testler, `Effect.md`/`ARCHITECTURE.md`.

## Bu Planın Dışında
ASC (container'ları sahiplenir, başlangıç effect'leri), Cooldown (HasDuration + `Cooldown.X` tag'i, kalan süre sorgusu), Cost: `Ability.md`. Seviye ölçekleme (`EffectSpec.Level`, `Magnitude.LevelValues`/`LevelCurve`, `ActiveEffect.Level`, `CalculationContext.Level`): `Level.md`.

## Bağımlılıklar
```
ActiveEffectContainer ──► ActiveEffect ──► EffectSpec ──► EffectData ──► Modifier ──► AttributeRegistry / AttributeId / ModifierOp
        │                                                    └──► ExecutionCalculation ──► CalculationContext, ExecutionOutput
        │                                       Magnitude ──► MagnitudeCalculation ──► CalculationContext
        ├──► AttributeContainer ──► AttributeSet ──► Aggregator, ModifierEventData ──► ModifierSource
        ├──► TagCountContainer, TagContainer, Tag (Tags)
        └──► TimerManager, TimerHandle (Timers)
```
Attribute katmanı effect sınıflarını bilmez (`ModifierSource.Data` tipi `Resource`).
Faz 3'te effect katmanı içinde tip döngüsü var: `EffectData`/`Magnitude` → `ExecutionCalculation`/`MagnitudeCalculation` → `CalculationContext` → `EffectSpec` → `EffectData`. Hesabın spec'e (SetByCaller, Instigator) erişmesi için gerekli, Unreal'da da böyle; katmanlar arasında döngü yok (**kullanıcı kararı**, 2026-11-17).

## Performans
- Zaman işi `TimerManager`'da: O(log n) kurma/durdurma, kare başına yalnızca süresi dolan timer'lar.
- Uygulama: süreli effect başına `ActiveEffect` + `float[]` + 2 delege; hazır spec ile Instant 0 allocation hedefi.
- Olaylar `in` struct ile; boxing yok.

## Test
Faz 0: mevcut 57 test (sourceId → `ModifierSource`), olay türleri ve sırası (hook → olay), container olay yayını, 0 allocation.
Faz 1: her op'ta Instant, süreli ekleme/geri alma, periyot 6/5 kuralı ve büyük delta, tag şartları, `RemoveEffectsWithTags`, SetByCaller eksik → 0, iç içe kaldırma. Testler headless Godot'ta çalışır.

## Uygulama Sırası
1. Faz 0: `ModifierSource`, `ModifierEventData` (+ `ModifierEventKind`, `ModifierEventHandler`), `Aggregator.EvaluateSingle`, `AttributeValue`/`AttributeSet`/`AttributeContainer` değişiklikleri, testler, `Attribute.md` güncellemesi.
2. Timers addon'u (repo kurulumu kullanıcıyla; kendi planı).
3. Tags: `BuildTagOptions` public.
4. Faz 1: `EffectHandle` → `Modifier` → `EffectData` → `EffectSpec` → `ActiveEffect` → `ActiveEffectContainer` → testler.
