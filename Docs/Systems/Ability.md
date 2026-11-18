# Ability Sistemi — Plan

> Durum: plan **kullanıcı onaylı**; Faz 1 **uygulandı** (2026-11-18), Godot içinde (headless) 71 test. UI'nin her kare sorduğu kontroller (`CanActivate`, `CooldownRemaining`, `CanAffordCost`, `IsOnCooldown`) 0 allocation. Faz 2 bekliyor.
>
> Uygulamada netleşenler (Faz 1):
> - `AbilityContainer(attributes, tags, effects, actor = null, abilitySystem = null)`: düğümler sonda ve isteğe bağlı.
> - ASC: `Attributes`, `Tags`, `Effects` constructor'da (editörde değil) kurulur; `Abilities` ve başlangıç içeriği ilk `_EnterTree`'de (hiçbir `_Ready`'den önce, `_Ready` değil). Yeniden parent'lama durumu korur.
> - `TryActivate(Ability)` tanımı da kopyayı da kabul eder; `Give(kopya)` reddedilir (hata).
> - `Activate` exception atarsa ability iptal edilir (owned tag'ler geri alınır), exception çağırana gider.
> - Owned tag dinleyicisi ability'yi aktifleşirken bitirirse `AbilityActivated` çalışmaz, `AbilityEnded` çalışır; `TryActivate` true döner.
> - Ekler: `Ability.IsOnCooldown`, `WaitDelayTask.Remaining` (cast bar). Bitmiş task'lar yeni task başlarken listeden düşer.
> Karşılığı: Unreal `UGameplayAbility`, `FGameplayAbilitySpec(Handle)`, `UAbilityTask`, `FGameplayEventData`, `UAbilitySystemComponent`.
> Ön koşul: PROGRESS'teki editör kontrolü (`[Tool]` olmayan alt sınıfı `.tres` olarak kaydedip yeniden açma). Ability'ler aynı yer tutucu mekanizmasına dayanıyor.

## Amaç
- Aktörün yapabildiği işler (büyü, saldırı, atılma): şartları (tag, cooldown, maliyet) kontrol et, maliyet ve cooldown uygula, zamana yayılan mantığı (bekleme, olay, giriş) çalıştır, bitince temizle.
- ASC aktörün attribute, tag, effect ve ability'lerini tek düğümde toplar.

## Kararlar
| # | Konu | Karar |
|---|---|---|
| K1 | Ability'nin yapısı | Kullanıcı soyut `Ability` Resource'undan `[GlobalClass]` alt sınıf yazar: ayarlar export, mantık override. `.tres` tanımdır (paylaşılır); verilince aktör başına `Duplicate()` ile kopya oluşur, durum kopyanın alanlarında tutulur (Unreal `InstancedPerActor`). Ayrı `AbilityData` yok. **Kullanıcı** |
| K2 | AbilityTask | Callback tabanlı (Unreal gibi): `WaitDelay(0.3f, Launch)`. Ability bitince çalışan task'lar durur, callback'leri gelmez. `await` desteği sonra bunun üstüne eklenebilir (IDEAS). **Kullanıcı** |
| K3 | Commit | `CommitOnActivate = true` (varsayılan): maliyet ve cooldown `Activate`'ten önce uygulanır. Kapalıysa kullanıcı `Commit()` çağırır (şarjlı yetenek bırakınca öder). Unreal'da her zaman elle. **Kullanıcı** |
| K4 | Cooldown | Ayrı `Cooldown` sınıfı yok (ARCHITECTURE'daki satır kalkar). `Ability.Cooldown` bir `EffectData`: HasDuration + `GrantedTags` (ör. `Cooldown.Fireball`); owner'da tag varken aktifleşmez. Maliyet de `EffectData` (Instant). İkisi de ability `.tres`'inin içinde gömülü alt resource olarak yaratılabilir, ayrı dosya gerekmez. **Kullanıcı** |
| K5 | Aynı ability ikinci kez verilirse | Yeni kayıt açılmaz, mevcut handle döner (tanım başına bir kayıt). **Kullanıcı** |
| K6 | Örnek sahne | Şimdilik yok; Faz 3 atlandı. Ability'ler headless testlerle doğrulanır. **Kullanıcı** |
| — | Bağımlılık | Ability, `AbilityContainer`'ı ve ASC'yi bilmez; ASC ona `Node` olarak verilir (`ModifierSource.InstigatorAbilitySystem` gibi). Task'lar Ability'yi bilmez. Döngü yok. |
| — | Örnekleme | Yalnızca aktör başına kopya; Ability aynı anda bir kez aktif olabilir. Örneksiz ve çalıştırma başına kopya ileride (IDEAS). |
| — | Hedefleme, ağ | Kapsam dışı. Hedefi kullanıcı kodu bulur (ör. merminin çarptığı düğüm). |

## Fazlar
| Faz | İçerik |
|---|---|
| 1 | `Ability`, `AbilityHandle`, `AbilityContainer`, `ActivationFailure`, `AbilityTask`, `WaitDelayTask`, `AbilitySystemComponent`; tag kuralları, cooldown, maliyet, commit, bitirme ve iptal. Effect/Attribute ekleri. |
| 2 | `AbilityEventData`, olay gönderme, tetikleyiciler, pasif ability, giriş; `WaitEventTask`, `WaitTagTask`, `WaitInputReleaseTask`. |
| 3 | (Atlandı, K6) Örnek sahne. |

## Faz 1 — Çekirdek

| Sınıf | Tür | Klasör |
|---|---|---|
| `Ability` | abstract Resource (`[GlobalClass]`) | `Ability/` |
| `AbilityHandle` | readonly struct (global pozitif sayaç, `None`) | `AbilityHandle/` |
| `AbilityContainer` + `ActivationFailure` | düz C# class + enum | `AbilityContainer/` |
| `AbilityTask` | abstract düz C# class | `AbilityTask/` |
| `WaitDelayTask` | düz C# class | `WaitDelayTask/` |
| `AbilitySystemComponent` | Node (`[Tool]`, `[GlobalClass]`) | `AbilitySystemComponent/` |

### Ability — inspector (gruplar)
- `Tags` (`TagContainer`): `AssetTags` (kimlik, ör. `Ability.Fireball`), `CancelAbilitiesWithTags` (aktifleşince eşleşen aktif ability'leri iptal eder), `BlockAbilitiesWithTags` (aktifken eşleşenler aktifleşemez), `ActivationOwnedTags` (aktifken owner'a), `ActivationRequiredTags` (hepsi), `ActivationBlockedTags` (biri varsa aktifleşmez). Eşleşme hiyerarşik (`Ability.Skill`, `Ability.Skill.Fireball`'ı tutar).
- `Commit`: `Cost` (`EffectData`, Instant beklenir), `Cooldown` (`EffectData`, HasDuration + GrantedTags beklenir), `CommitOnActivate = true`. Uymayan ayar → verilirken uyarı.
- Alt sınıfın kendi export'ları bunların altında. Tag şartları yalnızca aktifleşirken bakılır (sürekli değil, Unreal gibi).
- `_ValidateProperty` yok: `[Tool]` olmayan alt sınıf editörde yer tutucudur, kod çalışmaz. Tag menüleri `TagContainer`'ın kendisinden gelir.

### Ability — kullanıcı API'si
- Override: `protected abstract void Activate()`, `protected virtual void OnEnd(bool cancelled)` (temizlik), `protected virtual bool CanActivate()` (ek şart; false → `ActivationFailure.Custom`).
- Yardımcı: `bool Commit()` (cooldown ve maliyeti yeniden kontrol eder, uygular; yalnızca aktifken), `End()`, `Cancel()`, `EffectSpec MakeEffectSpec(EffectData data, Node causer = null)` (instigator = `Actor`, ASC, `SourceAttributes`, `SourceTags` dolu), `EffectHandle ApplyEffectToSelf(EffectData)`, `WaitDelayTask WaitDelay(float seconds, Action completed)`, `T RunTask<T>(T task)` (kullanıcının kendi task'ı).
- Okuma (verilmiş kopyada; `.tres` tanımında boş): `Definition`, `Handle`, `Level` (int, varsayılan 1; şimdilik yalnızca kullanıcı kodu için), `IsActive`, `Actor` (ASC'nin parent'ı), `AbilitySystem` (`Node`; `as AbilitySystemComponent`), `Attributes`, `Tags`, `Effects`, `CooldownRemaining`, `CanAffordCost`.
- Kopya per-actor olduğu için durum alanları serbesttir; export'lar çalışma anında değiştirilmez (alt resource'lar tanımla paylaşılır).

```csharp
[GlobalClass]
public partial class Fireball : Ability
{
    [Export] public PackedScene Projectile { get; set; }
    [Export] public EffectData Damage { get; set; }
    [Export] public float CastTime { get; set; } = 0.3f;

    // CommitOnActivate açık: mana ve cooldown burada zaten uygulanmış.
    protected override void Activate() => WaitDelay(CastTime, Launch);

    private void Launch()
    {
        var projectile = Projectile.Instantiate<FireballProjectile>();
        projectile.Hit = MakeEffectSpec(Damage, causer: projectile);
        Actor.GetParent().AddChild(projectile);
        End();
    }
}
// Mermi çarpınca: AbilitySystemComponent.Find(body)?.Effects.Apply(Hit);
// Oyuncu kodu:    _asc.Abilities.TryActivate(FireballData);
```

### Cooldown ve maliyet
- Cooldown kontrolü: `Tags.HasAny(Cooldown.GrantedTags)`. Kalan süre: `Effects.GetRemainingTime(Cooldown.GrantedTags)` (Effect eki). Tag tabanlı, böylece ortak cooldown (ör. tüm iksirler `Cooldown.Potion`) kendiliğinden çalışır.
- Maliyet kontrolü (Unreal gibi): `Cost`'un `Add` modifier'ları için `current + magnitude ≥ 0`; diğer op'lar ve execution'lar kontrol edilmez (gerekirse `CanActivate` override).
- Maliyet ve cooldown spec'leri verilirken bir kez kurulur, kontrol ve commit aynı spec'i kullanır: UI her kare `CanActivate`/`CooldownRemaining` sorsa da allocation yok. Sonuç: maliyette SetByCaller desteklenmez (Constant, AttributeBased, Custom kullanılır); seviyeye göre maliyet seviye/eğri işiyle gelir.
- Commit sırası (Unreal gibi): cooldown, sonra maliyet.

### AbilityContainer
- `AbilityContainer(AttributeContainer attributes, TagCountContainer tags, ActiveEffectContainer effects, Node actor = null, Node abilitySystem = null)`; ASC'siz de kurulabilir.
- `AbilityHandle Give(Ability definition, int level = 1)`: kopyalar, bağlar, `AbilityGiven`. Aynı tanım zaten verilmişse mevcut handle (K5). `bool Remove(AbilityHandle)`: aktifse iptal eder, sonra çıkarır, `AbilityRemoved`.
- `Ability Get(AbilityHandle)`, `Ability Find(Ability definition)`, `Count`, indexer.
- `bool CanActivate(handle)` ve `CanActivate(handle, out ActivationFailure reason)`; `bool TryActivate(AbilityHandle)`, `TryActivate(Ability definition)`, `TryActivateWithTag(Tag)` (AssetTags'ı eşleşen ilk aktifleşebilen; AI için).
- `bool Cancel(AbilityHandle)`, `int CancelWithTags(TagContainer)`, `CancelAll()`.
- Olaylar: `AbilityGiven(Ability)`, `AbilityRemoved(Ability)`, `AbilityActivated(Ability)`, `AbilityEnded(Ability, bool cancelled)`, `ActivationFailed(Ability, ActivationFailure)` (UI: "mana yetersiz").
- `ActivationFailure`: `NotGranted`, `AlreadyActive`, `TagRequirements`, `BlockedByAbility`, `Cooldown`, `Cost`, `Custom`.

**Aktifleşme:**
1. Kontrol sırası: verilmiş mi → zaten aktif mi → owner tag şartları → başka aktif ability'nin `BlockAbilitiesWithTags`'ı → cooldown → maliyet → `CanActivate()`. Tutmayan → `ActivationFailed`, false.
2. `CancelAbilitiesWithTags` ile eşleşen aktif ability'ler iptal (kendisi hariç).
3. `CommitOnActivate` → `Commit()`; başarısızsa `ActivationFailed`, false.
4. `IsActive`, `ActivationOwnedTags` → owner, `AbilityActivated`, `Activate()`.

**Bitirme** (`End`, `Cancel`, `Remove`, ASC silinirken): bitmiyorsa ve aktifse → çalışan task'lar durur → `OnEnd(cancelled)` → owned tag'ler geri alınır → `IsActive = false` → `AbilityEnded`. Bitirme sırasında aktifleşme denemesi reddedilir; ikinci `End` bir şey yapmaz.

**İç içe:** dinleyiciler ability verip kaldırabilir, aktifleştirip iptal edebilir. Tag ile iptal, her iptalden sonra listeyi baştan tarar ve çağrı sırasında aktifleşenlere dokunmaz (`RemoveEffectsWithTags` gibi). `Activate` içinde ability'yi bitirebilecek bir çağrıdan sonra kullanıcı `IsActive`'e bakar.

### AbilityTask
- `IsRunning`, `End()` (erken bitirme), `protected abstract void OnStart()`, `protected virtual void OnEnd()`. Ability `RunTask` ile başlatır, listesinde tutar, kendisi bitince hepsini `End` eder.
- Task Ability'yi bilmez; ihtiyacı olanı constructor'da alır. Faz 2 için temel sınıfta boş kancalar: `OnEvent(in AbilityEventData)`, `OnInputPressed()`, `OnInputReleased()`; Ability aktifken aldıklarını çalışan task'larına iletir.
- `WaitDelayTask(float seconds)`: `TimerManager` ile; `Completed` olayı. Ability bitince timer durur.
- Bellek: task başına bir nesne ve callback delegesi (aktifleşme sıklığında, kare başına değil).

### AbilitySystemComponent (Node)
- Sahip olduğu: `Attributes`, `Tags`, `Effects`, `Abilities`; `Actor` = parent. Mantık taşımaz, container'ları kurar ve bağlar.
- Export: `AttributeSets` (`string[]`, kayıtlı set adlarından menü), `StartingEffects` (`EffectData[]`, ör. başlangıç değerlerini Override eden Instant effect), `StartingAbilities` (`Resource[]`, menü `Ability` ile sınırlı, çalışma anında cast; yer tutucu kuralı).
- Constructor (editörde değil): `Attributes`, `Tags`, `Effects`. İlk `_EnterTree`: `Abilities` → set'ler → başlangıç effect'leri → başlangıç ability'leri. Tüm `_Ready`'ler hepsini hazır görür. Kodla eklenen set'lere başlangıç effect'i değmez (bunları ya export'ta seç ya effect'i kendin uygula).
- Silinirken (`NotificationPredelete`; yeniden parent'lamada değil): `Abilities.CancelAll()`, `Effects.RemoveAll()`. Böylece ölü aktörde timer çalışmaz.
- `static AbilitySystemComponent Find(Node node)`: düğümün kendisi veya doğrudan çocuğu; yoksa null.

### Effect ve Attribute ekleri
- `ActiveEffectContainer.GetRemainingTime(TagContainer grantedTags)`: bu tag'lerden birini veren etkin effect'lerin en uzun kalan süresi; yoksa 0 (cooldown ve buff UI'si).
- `ActiveEffectContainer.RemoveAll()`: tümünü normal kaldırma ile (olaylar çalışır).
- `AttributeRegistry`: set adları menüsü (editör) ve adla set oluşturma (parametresiz constructor).

## Faz 2 — Olaylar, Tetikleyiciler, Giriş

| Sınıf | Tür | Klasör |
|---|---|---|
| `AbilityEventData` + `AbilityEventHandler` | readonly struct + delegate | `AbilityEventData/` |
| `WaitEventTask`, `WaitTagTask`, `WaitInputReleaseTask` | düz C# class | kendi klasörleri |

- `AbilityEventData`: `Tag Tag`, `Node Instigator`, `Node Target`, `float Magnitude`, `object Payload`. ARCHITECTURE'daki `AbilityEvent` bu.
- `AbilityContainer.SendEvent(in AbilityEventData)`: önce tetiklenen ability'ler aktifleşir, sonra aktif ability'lerin task'larına iletilir, sonra `EventReceived`. Eşleşme hiyerarşik (`Event.Hit` tetikleyicisi `Event.Hit.Crit` ile de çalışır, Unreal gibi).
- Ability grubu `Triggers`: `TriggerEventTags` (olay gelince aktifleşir; `protected virtual void ActivateFromEvent(in AbilityEventData data) => Activate()`), `TriggerOnTagAdded` (owner'a tag gelince), `TriggerWhileTagPresent` (tag gelince aktifleşir, gidince iptal), `ActivateOnGranted` (pasif: verilince bir kez denenir). Tetikleyicisi olan ability yoksa tag dinlenmez.
- Giriş: `Ability.InputAction` (`[Export(PropertyHint.InputName)]`, yer tutucuda da menü çıkar), yuva değiştirmek için `Abilities.SetInput(handle, action)`. `Abilities.PressInput(StringName)`: aktif değilse dener, aktifse task'lara iletir; `ReleaseInput(StringName)`. ASC export `ProcessInput = false`: açıksa `_UnhandledInput`'ta bağlı action'ları kendisi iletir (oyuncu için).
- Task'lar: `WaitEventTask(Tag)` (olay verisiyle), `WaitTagTask(TagCountContainer, Tag, bool present)`, `WaitInputReleaseTask` (geçen süreyle; şarjlı yetenek). Ability yardımcıları: `WaitEvent`, `WaitTag`, `WaitInputRelease`.

## Bağımlılıklar
```
AbilitySystemComponent ──► AbilityContainer ──► Ability ──► WaitDelayTask, Wait*Task ──► AbilityTask ──► AbilityEventData
        │                                          └──► EffectSpec, ActiveEffectContainer, AttributeContainer, TagCountContainer
        └──► ActiveEffectContainer, AttributeContainer, TagCountContainer (Tags), TimerManager (Timers)
```
Ability, AbilityContainer'ı ve ASC'yi; task'lar Ability'yi bilmez. Effect ve Attribute katmanı Ability'yi bilmez.

## Performans
- Kare başına iş yok; ability'ler olay ve timer ile ilerler. `CanActivate`, `CooldownRemaining`, `CanAffordCost` 0 allocation (UI her kare sorabilir).
- Allocation yalnızca vermede (`Duplicate`, maliyet/cooldown spec'leri), task başlatmada ve kullanıcının yaptığı spec'lerde.
- Engelleme/iptal kontrolü aktif ability'ler üzerinde düz döngü (az sayıda).

## Test (headless Godot)
- Faz 1: verme/kaldırma ve aynı tanım; her `ActivationFailure` nedeni; owned/block/cancel tag'leri; commit (otomatik ve elle, sıra, başarısızlık); cooldown kalan süresi ve ortak cooldown; maliyet kontrolü; `End`/`Cancel` sırası ve tekrar; task'ların bitişte durması; iç içe iptal; ASC başlangıç sırası ve silinirken temizlik; kontrol sorgularında 0 allocation.
- Faz 2: olayla tetikleme (hiyerarşik), tag geldi/var tetikleyicileri, pasif, giriş basma/bırakma, üç task.

## Uygulama Sırası
1. Effect/Attribute ekleri. 2. `AbilityHandle`, `AbilityTask`, `WaitDelayTask`. 3. `Ability`. 4. `AbilityContainer`, `ActivationFailure`. 5. `AbilitySystemComponent`. 6. Faz 1 testleri, `ARCHITECTURE.md`, `IDEAS.md`. 7. Faz 2.

## Bu Planın Dışında (IDEAS'a taşınacak)
Örneksiz ve çalıştırma başına örnekleme, aktifken yeniden tetikleme (`bRetriggerInstancedAbility`), `await` ile task, animasyon task'ı (`PlayAnimationAndWait`), hedefleme, kaynak/hedef tag şartları, effect seviyesi ve eğri (maliyet/cooldown ölçekleme), aynı ability'yi birden fazla kaynağın vermesi (sayaç), başarısızlık tag'leri, animasyondan olay gönderme.
