# Cue Sistemi — Plan

> Durum: **uygulandı** (plan kullanıcı onaylı, 2026-11-18); 58 test, headless. Faz 2 (ability cue'ları, vuruş noktası) **onaylandı** (kullanıcı, 2026-11-19): C1–C6 öneriler gibi; **uygulandı**, derleme temiz, testler kullanıcıda.
>
> Faz 2 uygulamasında netleşenler:
> - Koddan cue parametresi: `new CueParameters(tag, target, instigator, causer, level)`, konum `WithLocation(point, normal)` ile (2D ve 3D). `CueContainer.Execute`/`Add`/`Remove` parametrenin tag'ini verilen tag'le değiştirir.
> - Ability yardımcıları 2D konum alır (`ExecuteCue(tag, location, normal)`); 3D için `Cues.Execute` doğrudan kullanılır. `AddCue` yalnızca aktifken (`RunTask` gibi); `RemoveCue` yalnızca ability'nin kendi eklediğini geri alır. Bitişte eklenenler sondan başa kalkar (`Finish`'in `finally`'sinde, owned tag'lerden önce).
> - `ActivationCues` owned tag'lerden sonra eklenir; `CommitOnActivate` başarısızsa ability zaten aktifleşmez.
> - Süren (Added) sahne konumu kullanmaz, aktörün çocuğu olarak açılır; konum yalnızca Executed sahnede.
> - 3D `RotateToNormal` sahnenin ölçeğini korur.
> Karşılığı: Unreal `GameplayCue` (`UGameplayCueManager`, `FGameplayCueParameters`, `AGameplayCueNotify_Actor`/`UGameplayCueNotify_Static`, `FGameplayEffectCue`). `Gameplay` ön eki yok.

## Amaç
- Effect'lere görsel/ses geri bildirimi: oyun mantığından ayrı, yalnızca görünüm. Effect veriyi değil cue tag'ini bilir (`Cue.Fire.Burn`); hangi sahnenin açılacağı ayrı bir kütüphanede.
- Instant ve periyodik çalıştırmada tek seferlik sahne (vuruş kıvılcımı), süreli/sonsuz effect boyunca süren sahne (yanma alevleri).

## Kararlar
| Konu | Karar |
|---|---|
| Kapsam | Yalnızca effect cue'ları. Ability cue'ları (`ExecuteCue`/`AddCue`), koddan cue API'si ve ASC cue olayı, Fireball örneğine cue: IDEAS. **Kullanıcı** |
| Kütüphane | Proje geneli tek `CueLibrary` `.tres`; yolu proje ayarında (`ability_system/cue_library_path`, varsayılan `res://Resource/Cues/Cues.tres`), Tags'teki `Tags.tres` gibi. Unreal'ın `GameplayCueManager`'ı gibi. **Kullanıcı** |
| Olay türleri | Üç: `Added`, `Executed`, `Removed`. Unreal'ın `WhileActive`'i ağ için (sonradan katılan); tek oyunculuda `OnActive` ile aynı anda gelir. **Kullanıcı** |
| Cue sahnesi | Herhangi bir sahne (kök `GPUParticles2D`, `AudioStreamPlayer`, `Node3D`…), kod gerekmez. Kök script `ICueNotify` arayüzünü uygularsa kancaları çağrılır. Ortak taban sınıf yok: C#'ta tek sınıftan türetilebildiği için kökün kendi tipini kapatırdı. **Kullanıcı** |
| Etkisiz effect | Etkisizleşince `Removed`, yeniden etkinleşince `Added` (modifier ve tag'lerle tutarlı). Etkisiz başlayan effect `Added` almaz. Unreal'daki karşılığı doğrulanamadı. **Kullanıcı** |
| Yığın | Yeni yığın cue'ya bir şey yapmaz; süren sahne açık kalır, yığın sayısı parametrede. **Kullanıcı** |
| Örnek sayısı | Süren cue hedef başına tag başına **tek** sahne; birden çok kaynak (iki ayrı yanma) sayaçla paylaşır, son kaynak kalkınca kalkar. Unreal `GameplayCueNotify_Actor` varsayılanı (doğrulandı). |
| Tag eşleşmesi | Kütüphanede tam tag yoksa en yakın parent'ın girişi kullanılır (`Cue.Fire.Burn` yoksa `Cue.Fire`), Unreal gibi. Cue tag'lerinin `Cue.` altında olması önerilir, zorunlu değil. |

## Sınıflar
| Sınıf | Tür | Klasör |
|---|---|---|
| `CueParameters` | readonly struct | `CueParameters/` |
| `ICueNotify` | interface (varsayılan gövdeli) | `ICueNotify/` |
| `CueEntry` | Resource (`[Tool]`, `[GlobalClass]`) | `CueEntry/` |
| `CueLibrary` | Resource (`[Tool]`, `[GlobalClass]`) | `CueLibrary/` |
| `CueContainer` | düz C# class | `CueContainer/` |

### EffectData
- Yeni grup `Cues`: `TagContainer Cues` (tüm türlerde görünür).
- Instant: her uygulamada `Executed`. Süreli/sonsuz: eklenince `Added`, kalkınca `Removed`; periyotluysa ayrıca her periyotta `Executed`.

### CueParameters
- `Tag Tag` (effect'teki cue tag'i), `Node Target` (cue'yu alan aktör), `Node Instigator`, `Node Causer`, `float Level`, `int StackCount`, `EffectSpec Spec`, `ActiveEffect Effect` (Instant'ta null). Düğümler null veya silinmiş olabilir. `Instigator`, `Causer`, `Level` spec'ten okunur.

### ICueNotify
```csharp
public interface ICueNotify
{
    void OnCueExecuted(in CueParameters parameters) { }
    void OnCueAdded(in CueParameters parameters) { }

    /// <summary>true: sahne kendini siler (ör. sönerek); false: hemen silinir.</summary>
    bool OnCueRemoved(in CueParameters parameters) => false;
}
```
Kök düğümün script'i uygular: `public partial class BurnFlames : GpuParticles2D, ICueNotify`.

### CueEntry ve CueLibrary
- `CueEntry`: `TagName` (tag menüsü, `TagContainer.BuildTagOptions`), `PackedScene Scene`, `float ExecutedLifetime = 2` (Executed sahnesinin ömrü, sn; 0 = sahne kendini siler), `bool AttachExecuted = false` (Executed sahnesi aktörün çocuğu olsun mu; varsayılan: aktörün parent'ına, aktörün konumunda).
- `CueLibrary`: `CueEntry[] Entries`. Statik `Default`: proje ayarındaki dosya (`GetLibraryPath()`) ilk kullanımda bir kez yüklenir; dosya yoksa boş kütüphane, cue'suz çalışılır (hata yok); dosya `CueLibrary` değilse hata, yine boş. `internal CueEntry Find(Tag tag)`: en derin eşleşen giriş (`TagRegistry.Matches`), sonuç tag başına önbellekte (yok da saklanır). Sahnesi boş giriş atlanır (parent'a düşülür).
- `AbilitySystemPlugin` proje ayarını kaydeder (Tags'in `tags/tag_list_path`'i gibi).

### CueContainer
- `CueContainer(Node target, ActiveEffectContainer effects, CueLibrary library = null)` (null → `CueLibrary.Default`). Effect olaylarına abone: `EffectAdded`, `EffectRemoved`, `EffectExecuted`, `InhibitionChanged`. Effect katmanı cue'ları bilmez.
- `Cues` boş effect için iş yok. Periyodik `EffectExecuted` aktif effect'i spec'iyle bulur (yığın sayısı için).
- **Executed:** sahne açılır; `AttachExecuted` ise aktörün çocuğu, değilse aktörün parent'ının çocuğu ve aktörün global konumunda (kök ve aktör ikisi de `Node2D` ya da `Node3D` ise). `OnCueExecuted`. `ExecutedLifetime > 0` ise o kadar sonra silinir (`TimerManager`, oyun duraklayınca durur).
- **Added:** tag'in sayacı 0 → 1 ise sahne aktörün çocuğu olarak açılır, `OnCueAdded`; değilse yalnızca sayaç artar.
- **Removed:** sayaç 1 → 0 ise `OnCueRemoved`; `false` dönerse (veya arayüz yoksa) `QueueFree`.
- `Detach()`: aboneliği bırakır ve sahneleri unutur, kanca çağırmaz. ASC silinirken (`NotificationPredelete`) en başta (`Abilities.CancelAll()` ve `Effects.RemoveAll()`'dan önce) çağrılır: aktörün çocuğu olan sahneler aktörle birlikte silinir, silinmekte olan düğümde kanca çalışmaz.
- **Ertelenmiş ekleme (uygulamada eklendi):** Godot, çocuklarını kurmakta olan düğüme `AddChild`'i reddeder (ör. ASC `_EnterTree`'de başlangıç effect'lerini uygularken aktör). Eklenecek parent ağaçta ve henüz hazır değilse (`!IsNodeReady()`) sahne kare sonunda eklenir (`CallDeferred`), kanca o zaman çağrılır. O arada `Removed` gelirse sahne hiç eklenmeden silinir, kanca çağrılmaz.
- **Hangi effect'in cue'su açık:** sayaç tag başına; ayrıca effect başına kaç cue tag'inin eklendiği tutulur (`GrantedTags`'teki gibi önce sayılır, sonra eklenir). Böylece kancadan effect'i kaldırmak, `InhibitionChanged`'in `EffectAdded`'den önce gelmesi veya `EffectAdded` gelmeden `EffectRemoved` gelmesi sayacı bozmaz.

### AbilitySystemComponent
- `Cues` (`CueContainer`), ilk `_EnterTree`'de diğer container'larla kurulur.

## Bağımlılıklar
```
AbilitySystemComponent ──► CueContainer ──► CueLibrary ──► CueEntry
                                │      └──► CueParameters, ICueNotify
                                └──► ActiveEffectContainer, ActiveEffect, EffectSpec, EffectData (olaylar)
                                └──► TimerManager (Timers), Tag/TagRegistry (Tags)
```
Effect katmanı cue katmanını bilmez (`EffectData.Cues` yalnızca tag verisi). Döngü yok.

## Performans
- Cue'suz effect: olay başına tek boş kontrol, 0 allocation.
- Cue olayı sahne açar (`Instantiate`): görsel iş, kare başına değil (periyodik vuruş başına). Havuz (pooling) IDEAS'ta.
- Kütüphane araması tag başına bir kez, sonra sözlükten.

## Test (headless, gerçek sahnelerle)
- Instant: Executed sahnesi açılır, konumu, ömür dolunca silinir; `AttachExecuted`.
- Süreli: Added → sahne aktörün çocuğu; Removed → silinir; `OnCueRemoved` true → silinmez.
- Periyodik: her periyotta Executed, ayrıca Added/Removed.
- İki kaynak aynı tag: tek sahne, ilk kaynak kalkınca kalır, ikincisiyle kalkar.
- Etkisizleşme Removed, etkinleşme Added; etkisiz başlayan Added almaz.
- Yığın eklenince bir şey olmaz; `StackCount` parametrede.
- Parent tag'e düşme; kütüphanede olmayan tag → hiçbir şey; kütüphane dosyası yok → hatasız.
- ASC: kurulum, aktör silinince kanca çağrılmadan temizlik; Fireball örneği (cue'suz) hatasız çalışır.
- Cue'suz effect'lerde 0 allocation.
- Uygulamada eklenenler: başlangıç effect'inin süren sahnesi kare sonunda eklenir (Executed sahnesi hazır parent'a hemen); eklenmeden kaldırılırsa hiç gösterilmez; `Default` proje ayarındaki dosyadan yüklenir; `Detach` sonrası kanca yok.
- Sonuç: 58 test geçti (test sahnesi `override.cfg` ile geçici tag listesi ve kütüphane yolu kullandı, sonra silindi). Fireball örneği ve ana sahne, kütüphane dosyası yokken hatasız çalıştı.

## Uygulama Sırası
1. `CueParameters`, `ICueNotify`. 2. `CueEntry`, `CueLibrary`, proje ayarı (`AbilitySystemPlugin`). 3. `EffectData.Cues`. 4. `CueContainer`. 5. ASC bağlantısı. 6. Testler. 7. Doküman: `ARCHITECTURE.md`, `Effect.md`, `IDEAS.md`, bu dosyanın durumu.

## Faz 2 — Ability Cue'ları ve Vuruş Noktası

Amaç:
- Ability kendi görselini effect'siz açabilsin: cast parlaması, kanal süresince aura.
- Vuruş sahnesi temas noktasında ve yüzey yönünde açılsın. Şu an Executed sahnesi hep aktörün konumunda açılıyor.

İkisi de `CueParameters`'ı değiştirdiği için tek plan. Hedefleme K5'in devamı. Karşılığı: Unreal `UGameplayAbility::ExecuteGameplayCue`/`AddGameplayCue` (ability bitince kalkar), `FGameplayCueParameters.Location`/`Normal`, `FGameplayEffectContext` içindeki hit result.

### Kararlar (Faz 2)
| # | Konu | Seçenekler | Öneri |
|---|---|---|---|
| C1 | Vuruş noktası effect'e nasıl ulaşır | (a) `EffectSpec`'e vuruş bilgisi: `HitPoint`, `HitNormal`, `HasHit` (`Vector3`, 2D kısayollarıyla) ve `SetHit(...)`, `Apply`'dan önce çağrılır. Spec paylaşıldığı için `ApplyEffectToTargets` noktası olan her hedefe spec'in bir kopyasını uygular. Unreal da spec'i hedef başına kopyalar ve hit result'ı context'e koyar. (b) `Apply(spec, hit)` aşırı yüklemesi: `EffectExecuted` imzası değişir, dinleyen herkes etkilenir. | (a). Kopya `SetByCaller` değerleriyle birlikte; hedef başına bir allocation (ability anında, kare başına değil). Mermi kendi spec'inde `SetHit` çağırır. |
| C2 | Executed sahnenin yeri | Konum varsa sahne orada açılır, yoksa bugünkü gibi aktörün konumunda. `AttachExecuted` açıkken de konum uygulanır: sahne aktörün çocuğu olur ama vuruş noktasında başlar. Normal için `CueEntry.RotateToNormal`: 2D'de sahnenin dönüşü normalin açısı olur; 3D'de sahnenin +Y'si normale bakar (kıvılcım, iz). | Bu; `RotateToNormal` varsayılan kapalı. Kanca normali parametreden de okuyabilir. |
| C3 | `CueParameters` | `Spec` artık null olabilir (koddan ya da ability'den gelen cue). `Instigator`, `Causer`, `Level` spec'ten okunmaz, saklanır. Yeni alanlar: `Location`, `Normal`, `HasLocation` (+ `Location2D`, `Normal2D`). Effect cue'su için kurucu aynı kalır; koddan cue için yeni kurucu. | Bu. Struct büyür ama `in` ile geçiyor. |
| C4 | Koddan cue API'si | `CueContainer.Execute(Tag, in CueParameters)`, `Add(Tag, in CueParameters)`, `Remove(Tag)`. Süren sahnenin sayacı effect cue'larıyla ortak: aynı tag tek sahne. Hedefte de cue açılabilsin diye `IAbilitySystem.Cues` eklenir (effect'siz vuruş kıvılcımı). Eşleşmeyen fazla `Remove` bir effect'in sahnesini de kapatabilir (dokümana not). | Bu. Aktörün tüm cue'lara kendi kodunda tepki vermesi (ASC cue olayı) IDEAS'ta kalır. |
| C5 | Ability cue'ları | Ability'ye `ExecuteCue`, `AddCue`, `RemoveCue` eklenir; `Tag` ve `TagContainer` alır, `ExecuteCue` isteğe bağlı konum ve normal de alır. Ability'nin eklediği cue'lar ability bitince kendiliğinden kalkar (Unreal gibi). Ek olarak `ActivationCues` (TagContainer) export'u: ability aktif olduğu sürece görünen cue, kodsuz cast parlaması için (`ActivationOwnedTags` gibi). | İkisi de. |
| C6 | Ability cue'lara nasıl ulaşır | `AbilityContainer` kurucusuna `CueContainer` parametresi eklenir; ASC verir. ASC'de `Cues`, `Abilities`'ten önce kurulur. Parametrede `Ability` alanı olmaz, çünkü cue katmanı ability'yi bilirse döngü oluşur. `Instigator` aktör, `Level` ability'nin seviyesidir. | Bu. Ability → CueContainer bağımlılığı oluşur, döngü yok. |

### Değişen sınıflar
- **`EffectSpec`:** `HitPoint`, `HitNormal`, `HasHit`, `SetHit(Vector2/Vector3 point, normal = default)`, `internal CopyWithHit(...)`.
- **`CueParameters`:** C3.
- **`CueEntry`:** `RotateToNormal`.
- **`CueContainer`:**
  - `Execute`, `Add`, `Remove`.
  - Executed sahnesini konuma yerleştirir. Effect cue'larında konum spec'in vuruşundan gelir.
- **`IAbilitySystem`:** `CueContainer Cues { get; }`.
- **`AbilityContainer`:** kurucuda `CueContainer`.
- **`AbilitySystemComponent`:** kurulum sırası (C6).
- **`Ability`:**
  - Yardımcı metotlar: `ExecuteCue`, `AddCue`, `RemoveCue` (C5).
  - `ActivationCues`.
  - Ability'nin eklediği cue'ların sayımı `_ownedTagCount` mantığıyla tutulur; `Finish`'te geri alınır.
  - `ApplyEffectToTargets`, noktası olan hedefe spec'in kopyasını uygular (C1).

### Kullanım
```csharp
// Cast: ActivationCues = Cue.Cast.Fire (aktifken el parlar). Vuruş: mermi temas noktasını spec'e yazar.
var spec = MakeEffectSpec(Damage, causer: projectile);
spec.SetHit(contactPoint, contactNormal);   // Damage.Cues = Cue.Hit.Fire → kıvılcım temas noktasında
target.Effects.Apply(spec);

// Effect olmadan hedefte görsel:
IAbilitySystem.Find(enemy)?.Cues.Execute(sparkTag, new CueParameters(sparkTag, enemy, instigator: Actor).WithLocation(point));
```

### Test
- **Vuruş noktası:** Executed sahnenin konumu (vuruşlu ve vuruşsuz); `AttachExecuted` ile konum; `RotateToNormal` 2D ve 3D.
- **`ApplyEffectToTargets`:** hedef başına kopya, her hedefte kendi noktası; noktasız hedefte paylaşılan spec; kopyada `SetByCaller` değerlerinin korunması.
- **Koddan cue:** `Execute`, `Add`, `Remove`; effect cue'su ile aynı tag'de tek sahne ve ortak sayaç; `IAbilitySystem.Cues`.
- **Ability cue'ları:** `ExecuteCue`, `AddCue`, `RemoveCue`; ability bitince (iptal dahil) eklenen cue'ların kalkması; `ActivationCues`.
- **Kenar durumlar:** kancadan ability'nin bitirilmesi; aktör silinirken temizlik.
- **Regresyon:** mevcut 58 cue testinin davranışı değişmez (konumsuz cue eskisi gibi).

### Uygulama sırası
1. `EffectSpec` vuruşu.
2. `CueParameters`, `CueEntry.RotateToNormal`.
3. `CueContainer` konum ve kod API'si.
4. `IAbilitySystem.Cues`, ASC kurulum sırası, `AbilityContainer`.
5. `Ability` cue yardımcıları, `ActivationCues`, `ApplyEffectToTargets` kopyası.
6. Doküman: `Effect.md` (spec vuruşu), `Targeting.md` (K5), `ARCHITECTURE.md`, `IDEAS.md`.

## Bu Planın Dışında (IDEAS)
ASC cue olayı (aktörün kendi tepkisi), Fireball örneğine cue, sahne havuzu, cue büyüklüğü (Unreal `RawMagnitude`/`MagnitudeAttribute`), yığın eklenince cue seçeneği.
