# Hedefleme Sistemi — Plan

> Durum: **onaylandı** (kullanıcı, 2026-11-18): K1–K7 öneriler gibi. Faz 1 ve 2 **uygulandı** (yalnızca derleme; testleri kullanıcı yapar). Faz 3 sonra ayrıca planlanacak.
>
> Uygulamada netleşenler:
> - `IAbilitySystem.FindInParents(Node)`: düğüm veya ASC'si olan en yakın atası (K6). `Find` ASC'si henüz kurulmamış (ağaca girmemiş) bileşeni de döner; `Effects` o zaman null, yardımcılar bunu atlar.
> - `TargetData` ekleri: `Add(in TargetHit)`, `CopyFrom(TargetData)` (olayla gelen veriyi saklamak için).
> - `TargetQuery2D`: sorgu başına en çok 64 collider (sabit `MaxColliders`); kendi filtresinden aynı sorguyu yeniden çalıştırmak hata verir (paylaşılan tampon). `RequireAbilitySystem` kapalıyken ASC'siz hedefte `RequiredTags` tutmaz, `BlockedTags` engellemez. Ray'de kaynak `CollisionObject2D` ise RID'i hariç tutulur (ışın ona takılmasın); Godot ışını içinden başladığı şekle zaten çarpmaz.
> - `Ability.ApplyEffectToTargets` aynı aktörü iki kez içeren veride iki kez uygular (sorgu zaten tekrar eklemez).
> Karşılığı: Unreal `FGameplayAbilityTargetData(Handle)` (`_SingleTargetHit`, `_ActorArray`), `UAbilityTask_WaitTargetData`, `AGameplayAbilityTargetActor_*`, `AGameplayAbilityWorldReticle`, `IAbilitySystemInterface`; sorgu kısmı UE5 Targeting System'den (seçim → filtre → sıralama) sadeleştirilmiş. `Gameplay` ön eki yok.

## Amaç
- Ability'nin kimi/nereyi etkilediğini tek bir veri tipiyle taşımak: aktör listesi ve/veya nokta (vuruş noktası, zemin hedefi).
- Sık kalıpları hazır vermek: "etrafımdaki düşmanlar", "önümdeki koni", "en yakın hedef"; hepsine tek çağrıyla effect uygulamak.
- AI'nın seçtiği hedefle ability aktifleştirmek; oyuncunun nişan alıp onaylaması (zemin hedefi).
- Şu anki yol (hedefi kullanıcı kodu bulur, ör. Fireball mermisi `AbilitySystemComponent.Find` ile) aynen çalışmaya devam eder; hedefleme isteğe bağlıdır.

## Kararlar (kullanıcı: hepsi "Öneri" sütunundaki gibi)
| # | Konu | Seçenekler | Öneri |
|---|---|---|---|
| K1 | Kapsam | Faz 1: hedef verisi, hedeflere effect uygulama, hedefle aktifleştirme. Faz 2: şekil sorgusu (alan, koni, ışın) ve filtreler. Faz 3: oyuncunun nişan alıp onayladığı hedefleme (nişangâh sahnesi, onay/iptal). | Faz 1 + 2 şimdi; Faz 3'ün planı Faz 2'den sonra ayrıca (giriş kararları ona bağlı). |
| K2 | 2D / 3D | (a) Yalnızca 2D. (b) Veri ortak, sorgu önce 2D, 3D sonra. (c) İkisi birden. Godot'ta fizik sorguları ayrı (`PhysicsDirectSpaceState2D`/`3D`, ayrı şekil tipleri), tek sınıfta birleşmez. | (b): `TargetHit` noktayı `Vector3` tutar, 2D için `Point2D`/`Normal2D`; sorgu sınıfı `TargetQuery2D`; `TargetQuery3D` IDEAS'a. |
| K3 | Hedefin effect/tag'lerine erişim | (a) Hedefleme ASC'nin üstünde bir katman: ASC'yi doğrudan kullanır, `Ability` tabanı hedeflemeyi bilmez (yardımcılar ability'de değil, `TargetData`/sorgu üzerinde; task yardımcısı yok, `RunTask` ile). (b) `IAbilitySystem` arayüzü (Unreal `IAbilitySystemInterface`): `Actor`, `Attributes`, `Tags`, `Effects` ve statik `Find(Node)`; ASC uygular. Alt katmanlar ASC yerine bunu bilir, döngü olmaz. | (b): `Ability`'ye `ApplyEffectToTargets`, AbilityEventData'ya hedef verisi, Faz 3'te `WaitTargetData` yardımcısı eklenebilir. Arayüz tek kullanımlık değil (sorgu, hedef verisi, ability yardımcıları), RULES ile çelişmez. |
| K4 | Hedefle aktifleştirme (AI) | (a) `AbilityEventData`'ya `TargetData Targets` alanı + `AbilityContainer.TryActivate(handle, in AbilityEventData)`; ability `ActivateFromEvent`'te okur (Unreal `TriggerAbilityFromGameplayEvent`). (b) Ayrı `TryActivate(handle, TargetData)` ve `Ability.ActivationTargets`. | (a): mevcut olay yoluyla aynı; olayla tetiklenen ability de hedef alabilir. |
| K5 | Vuruş noktası cue'ya | (a) Bu planda: `Apply`'a hedef başına isteğe bağlı vuruş bilgisi, `CueParameters.Location`/`Normal`; effect olaylarının imzası değişir. (b) Ayrı iş (IDEAS'ta kalır). | (b): effect katmanına dokunmamak için ayrı. |
| K6 | Collider → aktör | Sorgu collider bulur; aktör (ASC'nin parent'ı) bazen collider'ın kendisi (`CharacterBody2D`), bazen parent'ı (hitbox `Area2D` çocuğu). (a) Collider ya da en yakın atası, ASC'si olan ilk düğüm. (b) Yalnızca collider'ın kendisi. | (a); aynı aktör iki collider'la bulunursa bir kez eklenir. |
| K7 | Filtreler | Hazır: kendini hariç tut, ASC şartı, hedefte gerekli/yasak tag'ler (ör. yasak `State.Dead`), en çok N hedef, sıralama (yakın/uzak). Takım ilişkisi ("kaynağın düşmanı"), görüş hattı, özel filtre Resource'u IDEAS'a; koddan tek seferlik filtre `Func<Node, bool>` parametresiyle. | Bu liste. |

## Faz 1 — Hedef Verisi ve Uygulama

| Sınıf | Tür | Klasör |
|---|---|---|
| `TargetHit` | readonly struct | `TargetHit/` |
| `TargetData` | düz C# class | `TargetData/` |
| `IAbilitySystem` (K3-b) | interface | `IAbilitySystem/` |

### TargetHit
- `Node Actor` (zemin hedefinde null), `Vector3 Point`, `Vector3 Normal`, `bool HasPoint`; `Vector2 Point2D`, `Vector2 Normal2D` (K2-b). Düğüm null veya silinmiş olabilir.

### TargetData
- Bir hedefleme sonucu: `Count`, indexer (`TargetHit`), `Add(Node actor)`, `Add(Node actor, Vector2 point, Vector2 normal = default)`, `Add(Node actor, Vector3 point, Vector3 normal = default)`, `AddPoint(Vector2/Vector3 point)` (aktörsüz), `Contains(Node actor)`, `Clear()`.
- Yeniden kullanılabilir: ability bir örnek tutar, her kullanımda `Clear()`; liste büyüdükten sonra allocation yok.
- Hedef sırası korunur (sıralama sorguda yapılır).

### IAbilitySystem (K3-b)
```csharp
public interface IAbilitySystem
{
    Node Actor { get; }
    AttributeContainer Attributes { get; }
    TagCountContainer Tags { get; }
    ActiveEffectContainer Effects { get; }

    /// <summary>The node itself or its first direct child that implements this; null if none.</summary>
    static IAbilitySystem Find(Node node) { ... }
}
```
- `AbilitySystemComponent : Node, IAbilitySystem`. `AbilitySystemComponent.Find` aynı kalır (somut tip döner).
- `Ability.AbilitySystem` şimdilik `Node` kalır (kapsam dışı değişiklik yok).

### Ability ekleri
- `int ApplyEffectToTargets(EffectData data, TargetData targets, Node causer = null)`: tek spec (`MakeEffectSpec`), aktörü olan her hedefin effect'lerine; uygulanan hedef sayısını döner. Aynı spec birden çok hedefe uygulanabilir (`EffectSpec` bunu zaten destekliyor).
- `EffectHandle ApplyEffectToTarget(EffectData data, Node target, Node causer = null)`: tek hedef; ASC yoksa `None`.

### Hedefle aktifleştirme (K4-a)
- `AbilityEventData.Targets` (`TargetData`, null olabilir); constructor'a isteğe bağlı parametre.
- `AbilityContainer.TryActivate(AbilityHandle handle, in AbilityEventData data)`: normal şartlar; `ActivateFromEvent(data)` çağrılır. AI: hedefi seçer, `TargetData`'ya koyar, bununla aktifleştirir.
- Olay yoluyla gelen `TargetData` olay süresince geçerli kabul edilir; ability saklayacaksa kendi örneğine kopyalar (gönderen yeniden kullanabilir).

## Faz 2 — Şekil Sorgusu (2D)

| Sınıf | Tür | Klasör |
|---|---|---|
| `TargetQuery2D` | Resource (`[Tool]`, `[GlobalClass]`) | `TargetQuery2D/` |
| `TargetShape` | enum | `TargetQuery2D/` |
| `TargetSort` | enum | `TargetQuery2D/` |

### TargetQuery2D — inspector (gruplar)
- `Shape`: `Shape` (`Circle`, `Cone`, `Box`, `Ray`), `Range` (yarıçap / uzunluk), `Angle` (yalnızca Cone, derece), `Width` (yalnızca Box), `Offset` (kaynağa göre, yöne döner). İlgisiz alanlar gizlenir.
- `Collision`: `CollisionMask` (`PropertyHint.Layers2DPhysics`), `CollideWithBodies = true`, `CollideWithAreas = true`.
- `Filter` (K7): `ExcludeSource = true`, `RequireAbilitySystem = true`, `RequiredTags`, `BlockedTags` (hedefin tag'leri), `MaxTargets` (0 = sınırsız), `Sort` (`None`, `Nearest`, `Farthest`).

### Çalıştırma
- `int Run(Node2D source, Vector2 direction, TargetData into, Func<Node, bool> filter = null)`: kaynağın konumundan. `int RunAt(Node2D source, Vector2 origin, Vector2 direction, TargetData into, Func<Node, bool> filter = null)`: başka bir noktadan (zemin hedefi). `into`'ya ekler (temizlemez), eklenen sayıyı döner.
- Akış: şekil sorgusu (`IntersectShape`; Cone = daire + açı süzgeci; Ray = `IntersectRay`, ilk engel) → collider'dan aktör (K6) → filtreler → sıralama → `MaxTargets` → `into`.
- Nokta: Ray'de çarpma noktası ve normali; şekil sorgularında hedefin global konumu (Godot `IntersectShape` temas noktası vermez).
- Fizik durumu (`DirectSpaceState`) en doğru `_PhysicsProcess` içinde; başka yerden çağrılırsa son fizik karesinin sonucu (dokümana not).

### Kullanım
```csharp
[GlobalClass]
public partial class FlameNova : Ability
{
    [Export] public TargetQuery2D Area { get; set; }   // Circle 150, BlockedTags: State.Dead
    [Export] public EffectData Damage { get; set; }

    private readonly TargetData _targets = new();

    protected override void Activate()
    {
        _targets.Clear();
        Area.Run((Node2D)Actor, Vector2.Right, _targets);
        ApplyEffectToTargets(Damage, _targets);
        End();
    }
}
```

## Faz 3 — Oyuncunun Nişan Alması (taslak, K1'e bağlı)
- `WaitTargetDataTask`: nişangâh sahnesi (fareyi izler, menzille sınırlı) açar; onayda sorguyu nişangâh noktasında çalıştırıp `TargetData` verir, iptalde boş biter. Unreal `WaitTargetData` + `GroundTrace` + `WorldReticle`.
- Kararlar o zaman: onay/iptal girişi (ability'nin kendi tuşuna yeniden basmak mı, ASC'de ortak `ConfirmAction`/`CancelAction` mı), anında onay türü (AI ve hızlı nişan), nişangâh sahnesinin kancaları (cue'daki `ICueNotify` gibi).

## Bağımlılıklar (K3-b ile)
```
AbilitySystemComponent ──► IAbilitySystem (uygular)
Ability ──► TargetData ──► TargetHit
   └──► IAbilitySystem ──► ActiveEffectContainer, TagCountContainer, AttributeContainer
TargetQuery2D ──► TargetData, IAbilitySystem, TagContainer (Tags), Godot fizik
AbilityEventData ──► TargetData
```
- `IAbilitySystem`, `TargetData`, `TargetHit` Ability'yi ve ASC'yi bilmez. Döngü yok.

## Performans
- Kare başına iş yok; sorgu ability'nin çağırdığı anda.
- `TargetData` yeniden kullanılır; filtre, sıralama ve ekleme allocation'sız (sıralama ön ayrılmış tamponda).
- Godot fizik sorgusu sonucu `Godot.Collections.Array<Dictionary>` döner (Godot API, kaçınılamaz); sorgu parametre nesnesi Resource'ta bir kez kurulur. AI her kare sorgulamamalı (dokümana not).
- `ApplyEffectToTargets` tek spec ile; hedef başına yalnızca effect'in kendi uygulama maliyeti.

## Test (headless; testleri kullanıcı ayrı yapar)
- Faz 1: `TargetData` ekleme/temizleme/yeniden kullanma, aktörsüz nokta; `ApplyEffectToTargets` ASC'li/ASC'siz/silinmiş hedef, sayı; `IAbilitySystem.Find`; olayla ve `TryActivate(handle, data)` ile hedefin ability'ye ulaşması; tetiklenen ability'nin olayı.
- Faz 2: her şekil (koni açısı, kutu yönü, ışının ilk engeli ve normali), maske ve alan/gövde seçimi, collider → aktör (hitbox çocuğu, aynı aktörün iki collider'ı), filtreler (kendisi, ASC, gerekli/yasak tag, özel filtre), sıralama, `MaxTargets`, `RunAt`, eklenen sayı; yeniden kullanılan `TargetData`'da tekrar sorguda allocation yalnızca Godot sorgusundan.

## Uygulama Sırası
1. `TargetHit`, `TargetData`. 2. `IAbilitySystem`, ASC uygular. 3. `Ability` ekleri. 4. `AbilityEventData.Targets`, `TryActivate(handle, data)`. 5. `TargetQuery2D`, enum'lar. 6. Doküman: `ARCHITECTURE.md`, `Ability.md` (Kararlar'daki "Hedefleme kapsam dışı" satırı), `IDEAS.md`, bu dosyanın durumu.

## Bu Planın Dışında (IDEAS)
3D sorgu (`TargetQuery3D`), takım ilişkisi filtresi, görüş hattı, özel filtre/sıralama Resource'ları (UE5 Targeting System görev zinciri), vuruş noktasının cue'ya taşınması (K5), asenkron sorgu, kaynak konum bilgisi (Unreal `LocationInfo`, geri itme yönü için), ağ ve tahmin.
