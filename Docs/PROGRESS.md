# İlerleme — AbilitySystem

> Başlarken "Yapılıyor"a `- [ ] iş — [Yapan, YYYY-MM-DD]` ekle, bitince "Yapıldı"ya tek satır taşı. Ayrıntı git geçmişinde ve `Systems/` dokümanlarında.

## Yapıldı
- Addon iskeleti, `plugin.cfg`, `AbilitySystemPlugin` (boş)
- Attribute sistemi (`Systems/Attribute.md`), 57 test
- Effect planı (`Systems/Effect.md`) — [Claude, 2026-11-17]
- Effect Faz 0: `Aggregator` → `AttributeSet`, `ModifierSource`, `ModifierEventData`, `ExecuteModifier`, `PostEffectExecute`, `ModifierApplied`; 40 test — [Claude, 2026-11-17]
- Timers addon'u yazıldı (ayrı repo, `addons/Timers/`); effect'ler statik `TimerManager`'ı kullanacak — [Claude, 2026-11-17]
- Effect Faz 1: `EffectHandle`, `Modifier`, `EffectData`, `EffectSpec`, `ActiveEffect`, `ActiveEffectContainer`; Godot içinde 38 test — [Claude, 2026-11-17]
- `Magnitude` resource'u (Constant/SetByCaller/AttributeBased snapshot); süre ve periyot da Magnitude; `EffectData` grupları; 48 test — [Claude, 2026-11-17]
- `Magnitude.Snapshot` (kapalıysa her kullanımda okunur, süreli effect attribute'u takip eder), Pre/PostMultiplyAdd kaldırıldı; `Aggregator.SetMagnitude`, `UpdateModifier`, `ModifierEventKind.Updated`; 11 test — [Claude, 2026-11-17]

- Effect Faz 2: stacking (`ByTarget`/`BySource`, limit, süre/periyot yenileme, bitiş politikası); 18 test — [Claude, 2026-11-17]
- Effect Faz 3 planı (`Systems/Effect.md` → Faz 3), kullanıcı onaylı — [Claude, 2026-11-17]
- Effect Faz 3: Custom magnitude (`MagnitudeCalculation`), execution (`ExecutionCalculation`, `ExecutionOutput`), `CalculationContext`, inhibition, `RemovalTags`, bağışıklık; 60 test — [Claude, 2026-11-17]
- Editör denemesi (kullanıcı): gruplar, gizlenen alanlar, tag ve attribute menüleri, `Calculation` ve `Executions` menüleri çalışıyor; `[Tool]` olmayan hesaplama alt sınıfları için `Resource` tipli export ile düzeltildi — [Claude, 2026-11-17]
- Örnek sınıflar proje kökünde `Resource/` altında: `CombatSet`, `ArmorDamage`, `MissingHealthHeal` (repoya girmez) — [Claude, 2026-11-17]

## Yapılıyor
- (boş)

## Yapılacak (sırayla, `Systems/Effect.md` → Uygulama Sırası)
- [ ] Kullanıcı: Custom magnitude ve execution seçili bir `EffectData`'yı `.tres` olarak kaydedip yeniden aç, seçimler korunuyor mu kontrol et
- [ ] Ability, Cooldown, AbilityTask, ASC, örnek sahne (Fireball): önce `Systems/Ability.md` planı, kullanıcı onayıyla

## Notlar
- Testler headless Godot'ta: `Godot_..._console.exe --headless --path <proje> res://<test sahnesi>`; geçici test sahnesi proje kökünde açılıp sonra silinir. Timers eklentisi kapalıysa test `TimerManager` düğümünü kendisi ekler.
- Headless Godot çalıştırması yeni script'lerin `.cs.uid` dosyalarını da üretir; commit'e dahil et.
- `Attribute.md`'nin gövdesi Faz 0 öncesini anlatıyor; bir sonraki Attribute işinde güncellenmeli.
- Ana sahne (`run/main_scene`) yok; örnek sahne eklenince proje kökündeki `project.godot`'a girilmeli.
- Resource tabanlı attribute set fikri ertelendi (`Systems/Attribute.md` → İleride).
- Editörde `[Tool]` olmayan `[GlobalClass]` alt sınıf yalnızca yer tutucu `Resource` olur; alt sınıf beklenen export alanları `Resource` tipinde tutulup çalışma anında cast edilir (`Magnitude.Calculation`, `EffectData.Executions`).
- Faz 3 testleri, `Resource` tipine geçişten sonra yeniden çalıştırılmadı (yalnızca derleme).
- RULES "döngüsel bağımlılık yasak" ile Faz 3'teki katman içi tip döngüsü (`EffectData` → hesaplama → `CalculationContext` → `EffectSpec`) kullanıcı kararıyla kabul edildi (`Effect.md` → Bağımlılıklar).
