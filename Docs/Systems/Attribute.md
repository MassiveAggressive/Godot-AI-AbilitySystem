# Attribute Sistemi — Plan

> Durum: **kabul edildi** (2026-11-17), henüz uygulanmadı. Açık kalan kararlar Claude'un önerileriyle kapatıldı; ileride değişebilir (bkz. İleride Değerlendirilecek).
> Karşılığı: Unreal `FGameplayAttributeData`, `UAttributeSet`, `FGameplayAttribute`, `FAggregator`.

## Amaç
Aktörün sayısal değerleri (Health, MaxHealth, Mana, MoveSpeed...):
- Her attribute'un kalıcı bir **Base** değeri ve modifier'larla hesaplanan bir **Current** değeri vardır.
- Duration/Infinite effect'ler modifier ekler/kaldırır → Current değişir, Base değişmez. Instant effect'ler Base'i değiştirir.
- Kurallar (Health ≤ MaxHealth gibi) set sınıfında override ile yazılır.
- UI ve oyun kodu değişimleri C# event'leriyle dinler.

## Kararlar
| Konu | Karar |
|---|---|
| Tanımlama | Attribute'lar, `AttributeSet`'ten türeyen C# sınıflarında property olarak yazılır (Unreal gibi). Yeni attribute = kod. **Kullanıcı kararı.** |
| Veride seçim | EffectData/Modifier'da attribute `"SetAdı.AttributeAdı"` (ör. `CombatSet.Health`) string'i olarak açılır menüden seçilir; menü editörde reflection ile dolar. İlk kullanımda bir kez `AttributeId`'ye çevrilir. |
| Çarpanlar | İki ayrı işlem: `MultiplyAdditive` (toplanır, iki +%50 → ×2.0) ve `MultiplyCompound` (çarpılır, iki +%50 → ×2.25). **Kullanıcı kararı.** |
| Override | Birden fazla Override varsa en son eklenen geçerli; o kalkınca bir öncekine dönülür. |
| Değer tipi | `float` (Unreal ile aynı; Godot API'lerinin çoğu `float`). |
| Değer sınıfının adı | `AttributeValue`. `Attribute` adı `System.Attribute` ile çakışıyor: kullanıcı kodunda `using System;` + `using AbilitySystem;` olunca `Attribute` yazmak **CS0104** veriyor (derlenerek denendi). Set'ler kullanıcı kodunda yazılacağı ve `Math.Clamp` için `using System;` hemen her zaman olacağı için ad değiştirildi. |
| Başlangıç değerleri | Varsayılan değer koddadır (`new(100f)`). Aktöre özel değerler (Goblin 50 HP, Orc 200 HP) Unreal gibi **init effect** ile verilir: ASC'de `[Export]` bir başlangıç effect listesi, içinde Override modifier'lı Instant effect. Attribute sisteminde ek sınıf yok. Effect yazılana kadar kodla `SetBaseValue` kullanılır. |
| Başlatma | `AttributeRegistry` ilk kullanımda kendini kurar; autoload yok. |
| Ağ (replication) | Kapsam dışı. |

## Sınıflar

| Sınıf | Tür | Klasör | Sorumluluk |
|---|---|---|---|
| `ModifierOp` | `enum` | `ModifierOp/` | Modifier işlemi: `Add`, `MultiplyAdditive`, `MultiplyCompound`, `Override`. Hem Current hesabı hem (ileride) Modifier verisi kullanır. |
| `AttributeValue` | düz C# `sealed class` | `AttributeValue/` | Base + Current, aktif modifier listesi, Current hesabı, değişim olayları. Set'i **bilmez**. |
| `AttributeSet` | düz C# `abstract class` | `AttributeSet/` | Kullanıcının türettiği attribute grubu. Değer değiştirmenin giriş noktası; clamp/tepki hook'ları (override). |
| `AttributeId` | `readonly struct` | `AttributeId/` | Attribute'u veriden tanımlayan sayı (`CombatSet.Health` → 3). Effect'ler attribute'u bununla bulur. |
| `AttributeRegistry` | düz C# `static class` | `AttributeRegistry/` | `AttributeSet` alt sınıflarını reflection ile bir kez tarar; isim ↔ id eşlemesi, editör menüsü. |
| `AttributeContainer` | düz C# `sealed class` | `AttributeContainer/` | Aktörün set'leri. id ile O(1) attribute bulma ve id üzerinden değişiklik. `TagCountContainer`'ın attribute karşılığı. |

`AttributeContainer` neden ayrı: ASC mantık taşımaz (ARCHITECTURE.md). Effect'ler ASC'den önce yazılacak ve hedef olarak bu container'ı (ve `TagCountContainer`'ı) alacak.

## Ayrıntılar

### ModifierOp
```csharp
public enum ModifierOp { Add, MultiplyAdditive, MultiplyCompound, Override }
```
- Unreal 5.x'teki `DivideAdditive` / `AddFinal` yok: bölme `MultiplyCompound` ile `1/x` yazılır. İhtiyaç olursa eklenir.
- Godot'ta `[Export]` edilebilir (Modifier resource'u için).

### AttributeValue
- `public AttributeValue(float baseValue = 0f)`: Base ve Current başlangıçta aynı. Başlangıç değeri clamp'lenmez; varsayılanları tutarlı ver.
- `float BaseValue { get; }`, `float CurrentValue { get; }`: düz alan okuma.
- Değiştirme metotları `internal`; sadece `AttributeSet` çağırır (kurallar her değişimde çalışsın diye). Godot'ta addon ve oyun kodu aynı assembly'ye derlendiği için `internal` gerçek bir engel değil, niyeti belirtir.
- Aktif modifier'lar: `List<ModifierEntry>` (private nested struct: `ModifierOp Op`, `float Magnitude`, `int SourceId`). Liste ilk modifier geldiğinde bir kez oluşturulur; modifier almayan attribute bellek ayırmaz.
- `SourceId`: modifier'ı ekleyen kaynağın anahtarı (ileride aktif effect'in handle'ı). Attribute için anlamı yok, sadece kaldırmada kullanılır. Bir kaynak aynı attribute'a birden fazla modifier ekleyebilir; `RemoveModifiers(sourceId)` hepsini kaldırır.
- Kaldırma sırayı korur (Override'ın "son eklenen" kuralı için). Liste küçük olduğundan kaydırma ucuz.
- Current her değişimde listeden **baştan hesaplanır**, çalışan toplam tutulmaz. Böylece ekle/çıkar sırasında float hatası birikmez ve Override'ın kalkması doğru çalışır. Maliyet O(k), k = o attribute'taki aktif modifier sayısı (genelde < 10), allocation yok.
- Olaylar (C# `event`, sadece değer gerçekten değiştiyse):
  - `event Action<AttributeValue, float, float> BaseValueChanged`: (attribute, eski, yeni)
  - `event Action<AttributeValue, float, float> CurrentValueChanged`

#### Current formülü
```
Override varsa:  Current = son eklenen Override
yoksa:           Current = (Base + ΣAdd) × (1 + Σ(MultiplyAdditive − 1)) × ΠMultiplyCompound
```
- Büyüklük her çarpanda oran olarak yazılır: +%50 = `1.5`, −%20 = `0.8` (Unreal ile aynı).
- Örnek: Base 100, Add +20, MultiplyAdditive 1.5 ve 1.5, MultiplyCompound 1.1 → 120 × 2.0 × 1.1 = **264**.

### AttributeSet
```csharp
using System;
using AbilitySystem;

public sealed class CombatSet : AttributeSet
{
    public AttributeValue Health { get; } = new(100f);
    public AttributeValue MaxHealth { get; } = new(100f);

    protected override void PreBaseValueChange(AttributeValue attribute, ref float newValue) => ClampHealth(attribute, ref newValue);

    protected override void PreCurrentValueChange(AttributeValue attribute, ref float newValue) => ClampHealth(attribute, ref newValue);

    protected override void PostCurrentValueChange(AttributeValue attribute, float oldValue, float newValue)
    {
        // MaxHealth düşünce Health yeniden clamp'lenir.
        if (attribute == MaxHealth)
            SetBaseValue(Health, Health.BaseValue);
    }

    private void ClampHealth(AttributeValue attribute, ref float value)
    {
        if (attribute == Health)
            value = Math.Clamp(value, 0f, MaxHealth.CurrentValue);
    }
}
```

**Hook'lar** (`protected virtual`, varsayılanı boş):

| Hook | Ne zaman | Kullanım |
|---|---|---|
| `PreBaseValueChange(AttributeValue, ref float newValue)` | Base yazılmadan önce | Base'i clamp'le (Health ≥ 0). |
| `PreCurrentValueChange(AttributeValue, ref float newValue)` | Hesaplanan Current yazılmadan önce | Current'ı clamp'le. Modifier'ları değiştirmez; her hesapta yeniden uygulanır. |
| `PostCurrentValueChange(AttributeValue, float oldValue, float newValue)` | Current değiştikten sonra, olaylardan önce | Set içi tepki (MaxHealth → Health). |

Meta attribute'lar (Damage → Health) için `PostEffectExecute` hook'u Effect planında karara bağlanacak.

**Public API:**
- `void SetBaseValue(AttributeValue attribute, float value)`: Base'i yazar, Current'ı yeniden hesaplar. Değer aynı olsa da hesap yapılır (clamp yeniden uygulansın diye); olay yalnızca değişimde tetiklenir.
- `void AddModifier(AttributeValue attribute, ModifierOp op, float magnitude, int sourceId)`
- `void RemoveModifiers(AttributeValue attribute, int sourceId)`
- `attribute`'un bu set'e ait olduğu varsayılır, kontrol edilmez. Veri tarafı (id ile) doğru set'e `AttributeContainer` üzerinden yönlenir.

**Kurallar:**
- Attribute'lar `public AttributeValue X { get; } = new(...)` biçiminde **get-only public property** olarak yazılır. Registry sadece bunları görür (public alan veya private property görülmez).
- Set sınıfı adları proje genelinde benzersiz olmalı (menü namespace'siz `SetAdı.Attribute` gösterir). Çakışmada registry hata verir ve ikinci sınıfı atlar.
- Abstract veya generic set sınıfları taranmaz.
- Set bir Godot tipi değil (`Node`/`Resource` değil); `[Tool]` gerekmez.

### Değişim Akışı
```
SetBaseValue(attr, v)
  1. PreBaseValueChange(attr, ref v)
  2. attr.Base = v
  3. raw = formül(attr)
  4. PreCurrentValueChange(attr, ref raw)
  5. attr.Current = raw
  6. Current değiştiyse → PostCurrentValueChange(attr, eski, yeni)
  7. Olaylar: BaseValueChanged (değiştiyse), CurrentValueChanged (değiştiyse)

AddModifier / RemoveModifiers → listeyi güncelle, sonra 3–7 (Base olayı olmaz)
```
- Olaylar tüm değerler yazıldıktan sonra tetiklenir; dinleyici her zaman tutarlı durum görür (`TagCountContainer` ile aynı ilke).
- Sıra Unreal ile aynı: önce set'in hook'u, sonra dış dinleyiciler.
- Hook veya dinleyici başka bir attribute'u değiştirebilir (iç içe akış güvenli). Birbirini sonsuza kadar tetikleyen kurallar kullanıcının sorumluluğundadır.

### AttributeId
- `readonly struct AttributeId : IEquatable<AttributeId>` → `int Value`. `Value == 0` → `AttributeId.None`; `IsValid`.
- Sadece `AttributeRegistry` üretir (constructor `internal`).
- Boxing olmaması için `IEquatable<AttributeId>` + `GetHashCode() => Value`.
- `ToString()`: sadece debug için isim (`CombatSet.Health`).
- Id'ler çalışma anına özeldir; derlemeler arasında aynı kalmayabilir. Kaydedilen veride (resource) id değil isim tutulur.

### AttributeRegistry
- İlk erişimde bir kez: `typeof(AttributeSet).Assembly` içindeki abstract ve generic olmayan `AttributeSet` alt sınıflarını tarar. Godot'ta addon ve oyun kodu aynı assembly'ye derlenir. Set'ler ayrı bir kütüphanedeyse görülmez; ihtiyaç olursa genişletilir.
- Set'ler adına göre, attribute'lar tanım sırasına göre sıralanır; id'ler 1'den başlar.
- `AttributeId Request(string name)`: isimden id. Bulamazsa `AttributeId.None` + `GD.PushError`. Yükleme anında çağrılır, tick'te **çağrılmaz**.
- `string GetName(AttributeId id)`.
- `int Count`: container dizilerini boyutlandırmak için (id 0 dahil).
- `internal`: bir set tipinin id listesi ve property okuması (`AttributeContainer` bağlarken kullanır); `BuildOptions()`: editör menüsü için virgülle ayrılmış, sıralı isimler (Modifier resource'u kullanacak).
- İç veri: `string[] _names` (id ile indeksli), `Dictionary<string, int> _idsByName`, `Dictionary<Type, ...>` (set tipi → id listesi + `PropertyInfo[]`).
- Editörde oyun assembly'si her derlemede yeniden yüklendiği için statik durum sıfırlanır; menü yeni set'leri kendiliğinden görür.
- **Not:** iOS gibi NativeAOT/trimming kullanan dışa aktarımlarda reflection'a dikkat; gerekirse `[DynamicallyAccessedMembers]` eklenir.

### AttributeContainer
- Aktör başına bir tane. Oluşturulurken `AttributeRegistry.Count` boyutunda iki dizi bir kez ayrılır:
  - `AttributeValue[] _attributesById`
  - `AttributeSet[] _setsById`: attribute'un sahibi set (id ile yapılan değişikliği doğru set'in hook'larına yönlendirmek için)
  - ayrıca `List<AttributeSet> _sets`
- `bool Add(AttributeSet set)`: aynı tipte set zaten varsa hata + `false` (Unreal gibi: aktör başına her set tipinden bir tane). Registry'den set'in id'lerini alır, property'leri bir kez okuyup dizileri doldurur. Property `null` dönerse hata verir.
- Bir set örneği tek container'a eklenir (paylaşılırsa iki aktör aynı değerleri görür).
- `T Get<T>() where T : AttributeSet`: set listesinde tip kontrolü (genelde 1–3 set); yoksa `null`. Sık kullanan kod set referansını saklar.
- `bool Has(AttributeId id)`, `AttributeValue GetAttribute(AttributeId id)` (yoksa `null`): O(1).
- id ile değişiklik (Effect'ler için; sahibi set'e yönlendirir, hook'lar çalışır):
  - `SetBaseValue(AttributeId, float)`, `AddModifier(AttributeId, ModifierOp, float magnitude, int sourceId)`, `RemoveModifiers(AttributeId, int sourceId)`.
  - Aktörde o attribute yoksa işlem yok sayılır, hata verilmez: aynı effect farklı set'lere sahip aktörlere uygulanabilsin.
- Set kaldırma şimdilik yok.

### Kullanım
```csharp
var attributes = new AttributeContainer();
var combat = new CombatSet();
attributes.Add(combat);

combat.Health.CurrentValueChanged += OnHealthChanged;
combat.SetBaseValue(combat.Health, 50f);

// Effect tarafı (ileride): veri isimden bir kez id'ye çevrilir.
AttributeId health = AttributeRegistry.Request("CombatSet.Health");
attributes.AddModifier(health, ModifierOp.MultiplyAdditive, 1.5f, sourceId: 7);
attributes.RemoveModifiers(health, sourceId: 7);
```

## Bu Planın Dışında (ilgili planlarda)
- **Modifier verisi:** EffectData içinde `[Export] string Attribute` (açılır menü: `AttributeRegistry.BuildOptions`), `ModifierOp`, büyüklük → Modifier planı.
- **Instant effect'in Base'e uygulanması:** op'u Base üzerinde hesaplayıp `SetBaseValue` çağırmak → Effect planı.
- **Kaynak handle'ları:** `sourceId` üretimi. Elle modifier ekleyen kod, effect handle'larıyla çakışmayan id kullanmalı → Effect planında netleşir.
- **Attribute'a bağlı büyüklük** (MaxHealth = Vitality × 10) ve kaynak değişince modifier'ın güncellenmesi → Modifier/Effect planı.
- **Meta attribute** (Damage → Health) ve `PostEffectExecute` hook'u → Effect planı.
- **Aktöre editörden set ekleme** (ASC'de set tipi menüsü) ve **başlangıç effect listesi** → ASC planı.

## Bağımlılıklar
```
AttributeContainer ──► AttributeRegistry ──► AttributeId
        │                     │
        ▼                     ▼
   AttributeSet ◄──── (alt sınıfları tarar)
        │
        ▼
  AttributeValue ──► ModifierOp
```
- Döngü yok. `AttributeValue` set'i, `AttributeSet` registry'yi ve container'ı bilmez.
- Attribute sistemi Tags addon'unu kullanmaz.

## Performans Özeti
- Okuma (`CurrentValue`): alan okuma.
- Değişim: O(k) yeniden hesap; allocation, LINQ, boxing yok. Olaylar C# delegate.
- id ile bulma: O(1) dizi. Aktör başına 2 × `Count` referans (100 attribute ≈ 1,6 KB).
- Reflection yalnızca registry kurulumunda bir kez ve her `container.Add(set)`'te (set'in property'leri bir kez okunur; aktör oluşturma anı, tick değil). Çok sayıda aktör spawn eden oyunlarda darboğaz olursa derlenmiş getter'a geçilir.

## Uygulama Sırası
1. `ModifierOp`
2. `AttributeValue`
3. `AttributeSet`
4. `AttributeId`
5. `AttributeRegistry`
6. `AttributeContainer`

## İleride Değerlendirilecek
**Resource tabanlı set (kullanıcı fikri, 2026-11-17):** Set'ler editörde bir Resource olur, attribute'lar ve başlangıç değerleri bir dictionary ile girilir (`HealthSet` → `MaxHealth = 100`). Proje set'leri bilir, EffectData/Modifier menüsünde `HealthSet.MaxHealth` görünür. Şimdilik ertelendi; geçilirse:
- **Kazanç:** reflection kalkar (registry bir klasördeki set resource'larını tarar, Tags'teki gibi). Yeni attribute için kod gerekmez. Başlangıç değerleri doğrudan veride olur.
- **Paylaşım:** `.tres` yükleyen herkesle paylaşılır; canlı değer orada tutulamaz. Bu yüzden şablon (`AttributeSetData`, Resource) ve aktör başına çalışma zamanı örneği (`AttributeSet`, düz C#) ayrılmalı.
- **Kimlik:** Goblin ve Orc aynı set'i farklı değerlerle kullanacaksa set adı dosya adından gelemez (ya açık bir `Name` alanı ya da aktör başına değer ezme gerekir). Çözülmeden geçilmemeli.
- **Kurallar:** clamp ve özel mantık için ya isteğe bağlı bir C# sınıfı bağlanır (karma; öneri) ya da attribute başına min/max verisi tutulur.
- **Kayıp:** tipli erişim (`set.Health`) gider, yerine id veya önbelleğe alınmış referans gelir. Attribute'lar string anahtar olduğu için yeniden adlandırma referansları bozar.
- **Değişmeyenler:** `AttributeValue`, `ModifierOp`, `AttributeId`, `AttributeContainer`, Current formülü ve değişim akışı aynı kalır. Değişen kısım `AttributeSet` ve `AttributeRegistry`.
