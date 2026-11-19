# Hedefleme Sistemi — Plan

> Durum: **onaylandı** (kullanıcı, 2026-11-18): K1–K7 öneriler gibi. Faz 1 ve 2 **uygulandı** (70 test headless geçti). Faz 3 **onaylandı** (kullanıcı, 2026-11-19): K8–K14 öneriler gibi; örnek sahne yok (K15 çıkarıldı). Faz 3 **uygulandı**; 111 test headless geçti (Sonnet, 2026-11-19). Faz 4 (takım ilişkisi) **onaylandı** (kullanıcı, 2026-11-19): K16–K19 öneriler gibi; **uygulandı**, derleme temiz, testler kullanıcıda.
>
> Faz 4 uygulamasında netleşenler:
> - `TeamAttitudeMask`'te `None`/`All` üyesi yok: Godot inspector'da onları da ayrı onay kutusu gösterirdi. Varsayılan üçü birden.
> - Maske üçünü de içeriyorsa filtre hiç çalışmaz: kaynak sistemi aranmaz, eski davranış ve maliyet aynı kalır.
> - Kaynak ve hedef aynı sistemse `Friendly`, takım olmasa da (`ExcludeSource` kapalıyken kendini iyileştirme). Sistemi olmayan kaynak her hedefe `Neutral`.
> - `TeamName` çalışma anında değiştirilirse `Team` hemen yeniden çözülür. K5 (vuruş noktası cue'ya) artık `Cue.md` Faz 2'de.
>
> Faz 3 uygulamasında netleşenler:
> - Nişangâh, kaynağın (aktörün) çocuğu olarak eklenir. Onay anında fare yeniden okunur (`RefreshAim`), çünkü giriş olayı o karenin `_Process`'inden önce gelir. Menzile çekilen nokta küçük bir tolerans (0.01) ile menzil içinde sayılır.
> - `Instant` türünde (ve tuşsuz aktifleşen `InputRelease`'de) menzile çekme kapalıysa menzil dışı nişan `Cancelled` verir. `InputRelease`'de geçersiz nişanla tuşu bırakmak da iptal eder.
> - `Cancelled` ability başka bir nedenle bittiğinde gelmez. Nişangâhın `OnCancelled` kancası bu durumda da çalışır.
> - Task ayarları `init` property'leri; `Ability.WaitTargetData` `InputHeld`'i `IsInputPressed`'ten alır.
> - Test (111, headless) doğruladı: üç onay türü, menzile çekme (köşegen dahil, `Range = 0` sınırsız), çekmesiz menzil dışı onayın reddi, ability tuşu ve ortak onay/iptal, iki ability'nin birlikte onaylanması, nişangâh kancaları (`OnConfirmed` true dönünce kalır, `IsAimValid`, `CalculateAim`), yanlış kök ve sahnesiz nişangâh, dışarıdan bitirme ve kaynağın silinmesi, alan/ışın/koni sorguları, ASC action'ları.
> - Bilinen davranış: nişan geçersizken (çekmesiz menzil dışı, `IsAimValid` false) onay tıklaması reddedilir, bu yüzden yutulmaz; aynı action'a bağlı başka bir ability'yi tetikler (ör. sol tık hem onay hem saldırı ise). Değişiklik istenirse onay reddedilse bile yutulması mümkün; şimdilik tasarım (K14) gibi bırakıldı.
> - Headless test notu: fare konumu `Input.ParseInputEvent` ile verilir (`Viewport.PushInput` konumu değiştirmez) ve olay bir kare sonra işlenir.
>
> Uygulamada netleşenler:
> - `IAbilitySystem.FindInParents(Node)`: düğüm veya ASC'si olan en yakın atası (K6). `Find` ASC'si henüz kurulmamış (ağaca girmemiş) bileşeni de döner; `Effects` o zaman null, yardımcılar bunu atlar.
> - `TargetData` ekleri: `Add(in TargetHit)`, `CopyFrom(TargetData)` (olayla gelen veriyi saklamak için).
> - `TargetQuery2D`: sorgu başına en çok 64 collider (sabit `MaxColliders`); kendi filtresinden aynı sorguyu yeniden çalıştırmak hata verir (paylaşılan tampon). `RequireAbilitySystem` kapalıyken ASC'siz hedefte `RequiredTags` tutmaz, `BlockedTags` engellemez. Ray'de kaynak `CollisionObject2D` ise RID'i hariç tutulur (ışın ona takılmasın); Godot ışını içinden başladığı şekle zaten çarpmaz.
> - `Ability.ApplyEffectToTargets` aynı aktörü iki kez içeren veride iki kez uygular (sorgu zaten tekrar eklemez).
> - Test (70, headless) doğruladı: koni açısı hedefin merkez konumuna göre bakılır; şekil sorgularında nokta hedefin konumudur (temas noktası yok), yalnızca Ray çarpma noktası ve normali verir; Ray ilk çarptığı şey filtreye takılırsa arkasına geçmez (sonuç boş); sıfır yön sağa bakar; `Offset` yönle döner.
>
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

## Faz 3 — Oyuncunun Nişan Alması

Amaç: ability aktifken oyuncu fareyle bir nokta seçer (menzille sınırlı), onaylar veya iptal eder. Ability noktayı ve/veya o noktadaki hedefleri `TargetData` olarak alır. Karşılığı: Unreal `UAbilityTask_WaitTargetData` + `AGameplayAbilityTargetActor_GroundTrace` + `AGameplayAbilityWorldReticle` + ASC `LocalInputConfirm`/`LocalInputCancel`. AI bu yolu kullanmaz: hedefi kendi seçip `TryActivate(handle, data)` ile verir (K4).

### Kararlar (Faz 3)
| # | Konu | Seçenekler | Öneri |
|---|---|---|---|
| K8 | Onay/iptal girişi | (a) Yalnızca ability'nin kendi tuşuna yeniden basmak; iptal yok. (b) ASC'de ortak `ConfirmAction`/`CancelAction` (ör. sol tık / sağ tık, Esc). `AbilityContainer.InputConfirm()`/`InputCancel()` bunları koddan da (UI düğmesi) çağırır. Task'lara `OnConfirm`/`OnCancel` gider. (c) Her task'ın kendi action'ı: task'lar ham girişi almadığı için her kare yoklama gerekir. | (b). Ek olarak `Confirm` türünde ability'nin kendi tuşuna yeniden basmak da onaylar (task ayarı `AbilityInputConfirms = true`). |
| K9 | Onay türü (`TargetConfirmation`) | `Instant`: nişangâh açılmaz, o anki fare noktası hemen kullanılır (hızlı büyü). `Confirm`: onay girişine kadar bekler. `InputRelease`: tuşa basılı tutarken nişan alınır, bırakınca onaylanır. Unreal'daki `CustomMulti` (ör. arka arkaya 3 tuzak koymak) bu planda yok. | Bu üçü; `CustomMulti` IDEAS'a. `InputRelease` türünde ability tuşla aktifleşmemişse `Instant` gibi davranır (`WaitInputReleaseTask` ile aynı mantık). |
| K10 | Nişangâh sahnesi | (a) Sahnenin kökü `TargetReticle2D : Node2D`. Fareyi kendi `_Process`'inde izler; kancalar virtual metotlar. Görseller çocuk düğümlerdir, kod yazmak gerekmez. (b) Herhangi bir `Node2D` + isteğe bağlı arayüz (`ICueNotify` gibi). Bu durumda fareyi task izlemeli, task'a her kare çağrı gerekir. | (a). Sahne verilmezse görünmez bir `TargetReticle2D` yaratılır (görsel istemeyen için). |
| K11 | Nişangâh ağaçta nerede | (a) Aktörün çocuğu, `TopLevel = true` (dünya koordinatında durur, aktörle birlikte silinir). (b) Aktörün parent'ı (Fireball mermisi gibi). (c) Ayrı `CanvasLayer` (ekran koordinatı; kamera dönüşümü gerekir). | (a). |
| K12 | `TargetData` içeriği | Sorgu verilmemişse yalnızca nişan noktası (`AddPoint`). Sorgu verilmişse onay anında sorgu sonuçları: nişan noktasında `RunAt` (alan büyüsü) veya `QueryFromSource` açıkken kaynaktan nişana doğru (koni, çizgi). Her iki durumda yön kaynaktan nişana doğrudur. Nişan noktası `task.AimPoint`'ten de okunur. Alternatif: noktayı hep ilk kayıt olarak eklemek; ama o zaman `Count` hedef sayısı olmaz. | İlk paragraftaki gibi. `into`'ya ekler, temizlemez (`RunAt` gibi). |
| K13 | Menzil | (a) `ClampToRange = true`: nokta menzil sınırına çekilir. (b) Kapalıyken menzil dışında onay yok sayılır, nişan sürer. Ayrıca nişangâhın `IsAimValid`'i (ör. duvarın içi) onayı engelleyebilir. `Range <= 0` sınırsız demek. | İkisi de: varsayılan (a), kapatılınca (b). Geçersiz nişanın görseli nişangâhın işi. |
| K14 | Onay girişinin yutulması | Sol tık hem onay hem saldırı tuşu olabilir. (a) Onayı kabul eden bir task varsa olay ability'lere geçmez ve işlendi sayılır. Nişan yoksa sol tık normal çalışır. (b) Yutma yok. | (a). `OnConfirm`/`OnCancel` kabul edildiyse `true` döner. Olay tüm aktif ability'lerin task'larına gider; biri kabul ettiyse yutulur. |
### Sınıflar
| Sınıf | Tür | Klasör |
|---|---|---|
| `WaitTargetDataTask` | `AbilityTask` | `WaitTargetDataTask/` |
| `TargetConfirmation` | enum | `WaitTargetDataTask/` |
| `TargetReticle2D` | `Node2D` (`[GlobalClass]`) | `TargetReticle2D/` |

### TargetReticle2D
- Task başlarken kurar: `Source` (`Node2D`), `Range`, `ClampToRange`. Görsel menzil çemberi gibi şeyler bunları okur.
- `Vector2 AimPoint`: her kare `_Process`'te güncellenir. Varsayılan hesap: `GetGlobalMousePosition()`, ayara göre menzile çekilir. `GlobalPosition = AimPoint`.
- Kancalar (virtual, hepsi isteğe bağlı):
  - `protected virtual Vector2 CalculateAim(Vector2 mouse)`: ızgaraya yapıştırma, gamepad çubuğu vb. için.
  - `public virtual bool IsAimValid`: varsayılan menzil kontrolü.
  - `OnAimStarted()`.
  - `bool OnConfirmed()` / `bool OnCancelled()`: `true` dönerse sahneyi kendisi siler (ör. sönme animasyonu), `false` dönerse task hemen siler (`ICueNotify.OnCueRemoved` gibi).
- Her kare yapılan iş yalnızca fare konumu ve menzile çekme; allocation yok. Canlı hedef vurgulama (her kare sorgu) bu planda yok, alt sınıf isterse kendisi yapar.

### WaitTargetDataTask
- Constructor: `(Node2D source, PackedScene reticle, float range, TargetData into, TargetQuery2D query = null, TargetConfirmation confirmation = Confirm)`.
- Property'ler: `ClampToRange = true`, `QueryFromSource`, `AbilityInputConfirms = true`, `AimPoint`.
- Olaylar: `Confirmed` (`Action<TargetData>`) ve `Cancelled` (`Action`). Önce `End()`, sonra olay (AbilityTask kuralı).
- `OnStart`:
  - `Instant` türünde: noktayı hesaplar, hemen tamamlanır.
  - Diğer türlerde: sahneyi açar. Kök `TargetReticle2D` değilse hata verir ve iptal eder. Sahneyi aktörün çocuğu yapar (K11), sonra `OnAimStarted` çalışır.
- Girişler:
  - `OnConfirm`: nişan geçerliyse tamamlanır ve `true` döner. Geçersizse `false` döner ve nişan sürer.
  - `OnCancel`: `Cancelled` olur.
  - `OnInputPressed` (`AbilityInputConfirms` açıksa) / `OnInputReleased` (`InputRelease` türünde): onay gibi işler.
- `OnEnd`: nişangâh hâlâ duruyorsa siler. Ability herhangi bir nedenle bitince (stun, başka ability'nin iptali) nişangâh da gider.
- Sorgu onay anında çalışır: giriş olayında, yani son fizik karesinin durumuyla (Faz 2 notu).

### Mevcut sınıflara ekler
- `AbilityTask`: `protected virtual bool OnConfirm() => false`, `protected virtual bool OnCancel() => false`, ve `internal bool DeliverConfirm(bool confirm)`.
- `Ability`: `internal bool ReceiveConfirm(bool confirm)` (`ReceiveInput` gibi, `_deliveryDepth` ile) ve yardımcı metot:
  ```csharp
  WaitTargetData(PackedScene reticle, float range, TargetData into, Action<TargetData> confirmed, Action cancelled = null, TargetQuery2D query = null, TargetConfirmation confirmation = TargetConfirmation.Confirm)
  ```
  Kaynak `Actor as Node2D`. Değilse hata verir. Diğer ayarlar için task elle kurulur, olaylara abone olunur, sonra `RunTask` çağrılır. `Instant` `OnStart` içinde tamamlandığı için abonelik başlatmadan önce olmalı.
- `AbilityContainer`: `bool InputConfirm()` / `bool InputCancel()`. Tüm aktif ability'lere gider; bir task kabul ettiyse `true` döner.
- `AbilitySystemComponent`:
  - `[Export(PropertyHint.InputName)] StringName ConfirmAction` / `CancelAction` (boş bırakılabilir).
  - `_UnhandledInput` bunlara ability action'larından önce bakar. Kabul edildiyse olayı işlendi sayar ve ability'lere geçirmez (K14).

### Kullanım
```csharp
[GlobalClass]
public partial class Meteor : Ability
{
    [Export] public PackedScene Reticle { get; set; }   // kökü TargetReticle2D, çocuğu bir daire sprite'ı
    [Export] public TargetQuery2D Area { get; set; }    // Circle 120, BlockedTags: State.Dead
    [Export] public EffectData Damage { get; set; }
    [Export] public float Range { get; set; } = 600f;

    private readonly TargetData _targets = new();

    // CommitOnActivate kapalı: bedel ve bekleme süresi yalnızca onaylanınca.
    protected override void Activate()
    {
        _targets.Clear();
        WaitTargetData(Reticle, Range, _targets, Strike, Cancel, Area);
    }

    // AI: hedefi kendisi seçti, nişan yok.
    protected override void ActivateFromEvent(in AbilityEventData data)
    {
        _targets.Clear();
        _targets.CopyFrom(data.Targets);
        Strike(_targets);
    }

    private void Strike(TargetData targets)
    {
        if (!Commit())
        {
            Cancel();
            return;
        }

        ApplyEffectToTargets(Damage, targets);
        End();
    }
}
```
- Nişan sırasında başka ability'leri engellemek için mevcut yol kullanılır: `ActivationOwnedTags: State.Aiming` + diğerlerinde `ActivationBlockedTags`.

## Faz 4 — Takım İlişkisi

Amaç: alan saldırısı yalnızca düşmanı, iyileştirme yalnızca dostu bulsun. Şu an bunu ancak hedefin tag'leriyle yapabiliyoruz. Bu da "kaynağa göre düşman" bilgisini vermiyor: iki takım birbirine saldırınca aynı sorgu ikisinde de çalışmaz. Karşılığı: Unreal `IGenericTeamAgentInterface`, `FGenericTeamId`, `ETeamAttitude`.

### Kararlar (Faz 4)
| # | Konu | Seçenekler | Öneri |
|---|---|---|---|
| K16 | Takım nerede tutulur | (a) ASC'de: inspector'da tag menüsünden `TeamName`, çalışma anında `Tag Team { get; set; }`. `IAbilitySystem.Team` ile okunur. (b) Aktörün canlı tag'lerinde `Team.` altındaki tag: effect ile takım değişebilir (büyülenme). Ama aynı anda iki takım tag'i olabilir ve hangisinin geçerli olduğu belirsiz kalır. (c) `int TeamId`: menü yok, okunaksız. | (a). Geçici takım değişimi için kod `Team`'i değiştirir. Effect ile takım değiştirme IDEAS'a. |
| K17 | İlişki kuralı | Aynı takım: `Friendly`. İkisinin de takımı var ve farklı: `Hostile`. Birinin takımı yok: `Neutral`. Tam eşleşme: `Team.Monster.Orc` ile `Team.Monster.Goblin` birbirine düşman. Alternatifler: hiyerarşik ittifak (ortak ebeveyn dost sayılır) veya proje geneli ilişki tablosu (Unreal `SetAttitudeSolver`). | Basit kural. İttifak tablosu IDEAS'a. Takımsız aktör (hayvan, varil) herkese `Neutral`. |
| K18 | Sorgu filtresi | `TargetQuery2D` Filter grubuna `Attitudes` alanı: `[Flags] TeamAttitudeMask` (`Hostile`, `Neutral`, `Friendly`), varsayılan hepsi (filtresiz, bugünkü davranış). Kaynağın takımı onun ASC'sinden okunur; ASC yoksa kaynak takımsızdır. ASC'siz hedef (`RequireAbilitySystem` kapalı) `Neutral` sayılır. Kaynağın kendisi `ExcludeSource` ile ayrıca yönetilir; kapalıysa `Friendly` sayılır. | Bu. |
| K19 | Kod yardımcısı | AI ve özel filtreler için `static TeamAttitude IAbilitySystem.GetAttitude(IAbilitySystem a, IAbilitySystem b)` ve düğüm alan sürümü `GetAttitude(Node a, Node b)` (`FindInParents` ile). | İkisi. |

### Sınıflar ve ekler
| Sınıf | Tür | Klasör |
|---|---|---|
| `TeamAttitude` (`Friendly`, `Neutral`, `Hostile`) | enum | `TeamAttitude/` |
| `TeamAttitudeMask` (`Hostile = 1`, `Neutral = 2`, `Friendly = 4`) | `[Flags]` enum | `TeamAttitude/` |

- `IAbilitySystem`: `Tag Team { get; }`, `GetAttitude` (K19).
- `AbilitySystemComponent`:
  - `[Export] string TeamName`: tag menüsü; tag ağacı gelince oradan seçilir.
  - `Tag Team { get; set; }`: ilk `_EnterTree`'de isimden çözülür.
- `TargetQuery2D`: `Attitudes`. Kaynağın sistemi her `RunAt`'ta bir kez bulunur. `Accepts` içinde, tag kontrollerinden önce `int` karşılaştırması yapılır.

### Kullanım
```csharp
// FlameNova: Area.Attitudes = Hostile. Heal Circle: Attitudes = Friendly, ExcludeSource kapalı (kendini de iyileştirir).
if (IAbilitySystem.GetAttitude(Actor, enemy) == TeamAttitude.Hostile)
    abilities.TryActivate(attack, new AbilityEventData(attackTag, Actor, enemy));
```

### Test
- İlişki kuralının tüm durumları (aynı, farklı, biri takımsız, ikisi takımsız).
- Çalışma anında `Team` değişince sorgu sonucunun değişmesi.
- Sorguda her maske birleşimi; ASC'siz hedef; ASC'siz kaynak; `ExcludeSource` kapalıyken kaynağın `Friendly` sayılması.
- `TeamName` boş veya geçersiz.

### Uygulama sırası
1. `TeamAttitude`, `TeamAttitudeMask`.
2. `IAbilitySystem.Team`, `GetAttitude`.
3. ASC `TeamName`/`Team`.
4. `TargetQuery2D.Attitudes`.
5. Doküman.

## Bağımlılıklar (K3-b ile)
```
AbilitySystemComponent ──► IAbilitySystem (uygular)
Ability ──► TargetData ──► TargetHit
   └──► IAbilitySystem ──► ActiveEffectContainer, TagCountContainer, AttributeContainer
TargetQuery2D ──► TargetData, IAbilitySystem, TagContainer (Tags), Godot fizik
AbilityEventData ──► TargetData
```
- `IAbilitySystem`, `TargetData`, `TargetHit` Ability'yi ve ASC'yi bilmez. Döngü yok.
- Faz 3:
  ```
  Ability ──► WaitTargetDataTask ──► AbilityTask, TargetReticle2D, TargetQuery2D, TargetData
  AbilitySystemComponent ──► AbilityContainer.InputConfirm/InputCancel ──► Ability ──► AbilityTask
  ```
  `TargetReticle2D` yalnızca Godot'u bilir. Döngü yok.

## Performans
- Kare başına iş yok; sorgu ability'nin çağırdığı anda.
- `TargetData` yeniden kullanılır; filtre, sıralama ve ekleme allocation'sız (sıralama ön ayrılmış tamponda).
- Godot fizik sorgusu sonucu `Godot.Collections.Array<Dictionary>` döner (Godot API, kaçınılamaz); sorgu parametre nesnesi Resource'ta bir kez kurulur. AI her kare sorgulamamalı (dokümana not).
- `ApplyEffectToTargets` tek spec ile; hedef başına yalnızca effect'in kendi uygulama maliyeti.

## Test (headless; testleri kullanıcı ayrı yapar)
- Faz 1: `TargetData` ekleme/temizleme/yeniden kullanma, aktörsüz nokta; `ApplyEffectToTargets` ASC'li/ASC'siz/silinmiş hedef, sayı; `IAbilitySystem.Find`; olayla ve `TryActivate(handle, data)` ile hedefin ability'ye ulaşması; tetiklenen ability'nin olayı.
- Faz 2: her şekil (koni açısı, kutu yönü, ışının ilk engeli ve normali), maske ve alan/gövde seçimi, collider → aktör (hitbox çocuğu, aynı aktörün iki collider'ı), filtreler (kendisi, ASC, gerekli/yasak tag, özel filtre), sıralama, `MaxTargets`, `RunAt`, eklenen sayı; yeniden kullanılan `TargetData`'da tekrar sorguda allocation yalnızca Godot sorgusundan.
- Faz 3:
  - Üç onay türü. `InputRelease` türünde tuşsuz aktifleşmenin anında onaylanması.
  - Onay ve iptal: ortak action, koddan `InputConfirm`/`InputCancel`, ability'nin kendi tuşu.
  - Onayın yutulması: nişan varken sol tık saldırıyı tetiklemez, nişan yokken tetikler.
  - Menzile çekme açık/kapalı. Kapalıyken menzil dışındaki onayın yok sayılması; `IsAimValid`.
  - Sorgulu ve sorgusuz `TargetData`; `QueryFromSource`.
  - Nişangâhın aktörün çocuğu olarak açılması ve ability bitince/iptal edilince silinmesi. `OnConfirmed` `true` dönünce silinmemesi.
  - Kökü yanlış tipte sahnenin hata vermesi; sahnesiz görünmez nişangâh.
  - Aktör silinince sorun çıkmaması.

## Uygulama Sırası
1. `TargetHit`, `TargetData`. 2. `IAbilitySystem`, ASC uygular. 3. `Ability` ekleri. 4. `AbilityEventData.Targets`, `TryActivate(handle, data)`. 5. `TargetQuery2D`, enum'lar. 6. Doküman: `ARCHITECTURE.md`, `Ability.md` (Kararlar'daki "Hedefleme kapsam dışı" satırı), `IDEAS.md`, bu dosyanın durumu.
- Faz 3:
  1. `AbilityTask`, `Ability` ve `AbilityContainer`'a onay/iptal iletimi.
  2. ASC'de `ConfirmAction`/`CancelAction`.
  3. `TargetReticle2D`.
  4. `WaitTargetDataTask`, `TargetConfirmation`, `Ability.WaitTargetData`.
  5. `--import` ile `.cs.uid` dosyaları.
  6. Doküman.

## Bu Planın Dışında (IDEAS)
3D sorgu (`TargetQuery3D`), takım ittifak tablosu ve effect ile takım değiştirme (Faz 4'ten), görüş hattı, özel filtre/sıralama Resource'ları (UE5 Targeting System görev zinciri), asenkron sorgu, kaynak konum bilgisi (Unreal `LocationInfo`, geri itme yönü için), ağ ve tahmin. Faz 3'ten: çoklu onay (`CustomMulti`), nişan sırasında hedef vurgulama, hazır gamepad nişanı, 3D nişangâh.
