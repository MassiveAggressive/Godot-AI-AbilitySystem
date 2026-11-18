# Cue Sistemi — Plan

> Durum: **plan**, kararlar kullanıcıdan alındı (2026-11-18); onay bekliyor, uygulama başlamadı.
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
- `Tag Tag` (effect'teki cue tag'i), `Node Target` (cue'yu alan aktör), `Node Instigator`, `Node Causer`, `float Level`, `int StackCount`, `EffectSpec Spec`, `ActiveEffect Effect` (Instant'ta null). Düğümler null veya silinmiş olabilir.

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
- `CueLibrary`: `CueEntry[] Entries`. Statik `Default`: proje ayarındaki dosya ilk kullanımda bir kez yüklenir; dosya yoksa cue'suz çalışılır (hata yok). `internal CueEntry Find(Tag tag)`: en derin eşleşen giriş (`TagRegistry.Matches`), sonuç tag başına önbellekte (yok da saklanır).
- `AbilitySystemPlugin` proje ayarını kaydeder (Tags'in `tags/tag_list_path`'i gibi).

### CueContainer
- `CueContainer(Node target, ActiveEffectContainer effects, CueLibrary library = null)` (null → `CueLibrary.Default`). Effect olaylarına abone: `EffectAdded`, `EffectRemoved`, `EffectExecuted`, `InhibitionChanged`. Effect katmanı cue'ları bilmez.
- `Cues` boş effect için iş yok. Periyodik `EffectExecuted` aktif effect'i spec'iyle bulur (yığın sayısı için).
- **Executed:** sahne açılır; `AttachExecuted` ise aktörün çocuğu, değilse aktörün parent'ının çocuğu ve aktörün global konumunda (kök ve aktör ikisi de `Node2D` ya da `Node3D` ise). `OnCueExecuted`. `ExecutedLifetime > 0` ise o kadar sonra silinir (`TimerManager`, oyun duraklayınca durur).
- **Added:** tag'in sayacı 0 → 1 ise sahne aktörün çocuğu olarak açılır, `OnCueAdded`; değilse yalnızca sayaç artar.
- **Removed:** sayaç 1 → 0 ise `OnCueRemoved`; `false` dönerse (veya arayüz yoksa) `QueueFree`.
- `Detach()`: aboneliği bırakır ve sahneleri unutur, kanca çağırmaz. ASC silinirken (`NotificationPredelete`) `Effects.RemoveAll()`'dan önce çağrılır: aktörün çocuğu olan sahneler aktörle birlikte silinir, silinmekte olan düğümde kanca çalışmaz.

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

## Uygulama Sırası
1. `CueParameters`, `ICueNotify`. 2. `CueEntry`, `CueLibrary`, proje ayarı (`AbilitySystemPlugin`). 3. `EffectData.Cues`. 4. `CueContainer`. 5. ASC bağlantısı. 6. Testler. 7. Doküman: `ARCHITECTURE.md`, `Effect.md`, `IDEAS.md`, bu dosyanın durumu.

## Bu Planın Dışında (IDEAS)
Ability cue'ları, koddan cue API'si ve ASC cue olayı (aktörün kendi tepkisi), Fireball örneğine cue, sahne havuzu, cue büyüklüğü (Unreal `RawMagnitude`/`MagnitudeAttribute`), yığın eklenince cue seçeneği.
